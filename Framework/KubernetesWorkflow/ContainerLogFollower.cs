using k8s;
using Logging;
using Utils;

namespace KubernetesWorkflow
{
    // Continuously follows a container's log and appends it to a file, so logs
    // are captured even when a test hangs or is killed before the teardown-time
    // log download runs. The pod is resolved lazily from its deployment's pod
    // label inside the worker, so constructing and starting the follower never
    // blocks the startup path. When the follow stream ends (container
    // stop/restart) it appends the tail of the previous container instance,
    // which holds the crash output, then re-resolves and re-follows.
    public class ContainerLogFollower
    {
        private const int PreviousTailLines = 500;
        private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(2);

        private readonly ILog log;
        private readonly KubernetesClientConfiguration config;
        private readonly string containerName;
        private readonly string podLabel;
        private readonly string recipeName;
        private readonly string k8sNamespace;
        private readonly BufferedFollowLogHandler handler;
        private CancellationTokenSource cts;
        private Task? worker;

        public ContainerLogFollower(ILog log, KubernetesClientConfiguration config, string containerName, string podLabel, string recipeName, string k8sNamespace)
        {
            this.log = log;
            this.config = config;
            this.containerName = containerName;
            this.podLabel = podLabel;
            this.recipeName = recipeName;
            this.k8sNamespace = k8sNamespace;
            cts = new CancellationTokenSource();
            handler = new BufferedFollowLogHandler(log, "Following log for " + containerName, containerName + "_follow");
        }

        public LogFile LogFile => handler.LogFile;
        public long LinesWritten => handler.LinesWritten;
        public DateTimeOffset LastWriteUtc => handler.LastWriteUtc;

        // False only when the follow file is definitely unusable: the worker died
        // after Start (e.g. cluster unreachable from the runner) or nothing was
        // ever captured. Silence alone is NOT unhealthy: a quiet but alive follower
        // may hold the complete pre-rotation history, which a fallback cluster
        // fetch cannot recover.
        public bool IsCaptureHealthy =>
            worker is { IsCompleted: false } &&
            handler.LinesWritten > 0;

        public void Start()
        {
            if (worker != null) throw new InvalidOperationException();

            cts = new CancellationTokenSource();
            worker = Task.Run(Worker);
        }

        public void Stop()
        {
            if (worker == null) return;

            cts.Cancel();
            // Cancellation disposes the follow stream, which unblocks the
            // synchronous read. Wait boundedly so teardown cannot hang on an
            // unreachable cluster.
            if (!worker.Wait(TimeSpan.FromSeconds(10)))
                log.Error($"Log follower for '{containerName}' did not stop in time.");
            worker = null;
        }

        private void Worker()
        {
            try
            {
                using var client = new Kubernetes(config);
                string? podName = null;

                while (!cts.IsCancellationRequested)
                {
                if (podName == null)
                {
                    podName = ResolvePodName(client);
                    if (podName == null)
                    {
                        // Pod is not scheduled yet. Keep waiting: the whole
                        // point is to capture the log from the very first line.
                        cts.Token.WaitHandle.WaitOne(ReconnectDelay);
                        continue;
                    }
                }

                try
                {
                    FollowCurrentContainer(client, podName);
                }
                catch (Exception) when (cts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    log.Error($"Log follow interrupted for '{containerName}': {ex.Message}");
                }

                if (cts.IsCancellationRequested) return;

                try
                {
                    using var previous = client.ReadNamespacedPodLog(podName, k8sNamespace, recipeName, tailLines: PreviousTailLines, previous: true, timestamps: true);
                    handler.Log(previous);
                }
                catch (Exception)
                {
                    // No previous container instance to collect from.
                }

                // Re-resolve: the pod may have been recreated under a new name.
                podName = null;
                cts.Token.WaitHandle.WaitOne(ReconnectDelay);
                }
            }
            finally
            {
                handler.Dispose();
            }
        }

        private string? ResolvePodName(Kubernetes client)
        {
            try
            {
                var pods = client.ListNamespacedPod(k8sNamespace);
                var pod = pods.Items.FirstOrDefault(p =>
                    p.Metadata?.Labels != null &&
                    p.Metadata.Labels.TryGetValue(K8sController.PodLabelKey, out var label) &&
                    label == podLabel);
                return pod?.Metadata?.Name;
            }
            catch (Exception ex)
            {
                log.Error($"Failed to resolve pod for '{containerName}': {ex.Message}");
                return null;
            }
        }

        private void FollowCurrentContainer(Kubernetes client, string podName)
        {
            // sinceSeconds resumes shortly before the last line we wrote, so a
            // reconnect does not replay the whole log. First connection has no
            // cursor and captures the full log. The +1s boundary epsilon may
            // redeliver lines; the handler drops exact repeats.
            var sinceSeconds = handler.LastTimestamp.HasValue
                ? (int)Math.Min(int.MaxValue,
                    (DateTime.UtcNow - handler.LastTimestamp.Value.UtcDateTime).TotalSeconds + 1)
                : (int?)null;
            using var stream = client.ReadNamespacedPodLog(
                podName, k8sNamespace, recipeName,
                follow: true, timestamps: true,
                sinceSeconds: sinceSeconds);
            using (cts.Token.Register(() =>
            {
                try { stream.Dispose(); }
                catch { /* best effort: unblocks the reader */ }
            }))
            {
                handler.Log(stream);
            }
        }

        // LogHandler that writes through a persistent writer with AutoFlush,
        // so an abrupt kill of the test runner cannot lose buffered lines.
        // WriteToFileLogHandler does File.AppendAllLines per line (an
        // open/write/close per log line), which is too much syscall churn
        // for high-volume trace streams.
        internal class BufferedFollowLogHandler : LogHandler, IDisposable
        {
            private readonly ILog sourceLog;
            private readonly StreamWriter writer;

            public BufferedFollowLogHandler(ILog sourceLog, string description, string addFileName)
            {
                this.sourceLog = sourceLog;
                LogFile = sourceLog.CreateSubfile(addFileName);

                var msg = $"{description} -->> {LogFile.Filename}";
                sourceLog.Log(msg);
                LogFile.Write(msg);
                LogFile.Write(description);

                writer = new StreamWriter(LogFile.Filename, append: true) { AutoFlush = true };
            }

            public LogFile LogFile { get; }

            // Monotonic cursor over the Kubernetes log timestamps. The
            // previous-instance tail overlaps already-seen lines, so the
            // cursor only ever moves forward.
            public DateTimeOffset? LastTimestamp { get; private set; }

            // Written by the follower worker, read during teardown: interlocked
            // to prevent torn multiword reads.
            private long linesWritten;
            private long lastWriteUtcTicks;

            public long LinesWritten => Interlocked.Read(ref linesWritten);
            public DateTimeOffset LastWriteUtc => new DateTimeOffset(Interlocked.Read(ref lastWriteUtcTicks), TimeSpan.Zero);

            public void Dispose()
            {
                writer.Dispose();
            }

            // Exact repeats of recently written lines are dropped: reconnects
            // (sinceSeconds epsilon) and the previous-instance tail redeliver them.
            // Suppression is NEVER based on timestamp alone - distinct lines can
            // share a coarse clock tick. Overlaps beyond the window are written
            // again: duplicates are always preferable to lost lines.
            private const int RecentLineWindow = 4096;
            private readonly Queue<string> recentLineQueue = new();
            private readonly HashSet<string> recentLineSet = new();

            protected override void ProcessLine(string line)
            {
                // Same filtering as WriteToFileLogHandler: these lines are not
                // useful and have no topic so normal log-level controls skip them.
                if (line.Contains("Received JSON-RPC response") && !line.Contains("topics=")) return;
                if (line.Contains("object field not marked with serialize, skipping")) return;

                var raw = line;
                if (recentLineSet.Contains(raw)) return;

                // All streams are read with timestamps=true, so every line starts with
                // the Kubernetes log timestamp. It drives the monotonic cursor and is
                // then stripped, keeping the file format identical to DownloadPodLog
                // output so teardown consumers (e.g. the transcript converter) can parse
                // follow files interchangeably.
                var spaceIndex = line.IndexOf(' ');
                if (spaceIndex > 0 &&
                    DateTimeOffset.TryParse(line.AsSpan(0, spaceIndex), out var timestamp))
                {
                    // Monotonic cursor over the Kubernetes log timestamps.
                    // The previous-instance tail predates already-seen lines,
                    // so the cursor only ever moves forward.
                    if (!LastTimestamp.HasValue || timestamp > LastTimestamp.Value)
                        LastTimestamp = timestamp;
                    line = line.Substring(spaceIndex + 1);
                }

                line = sourceLog.ApplyStringReplace(line);
                writer.WriteLine(line);
                Interlocked.Increment(ref linesWritten);
                Interlocked.Exchange(ref lastWriteUtcTicks, DateTimeOffset.UtcNow.Ticks);

                recentLineQueue.Enqueue(raw);
                recentLineSet.Add(raw);
                if (recentLineQueue.Count > RecentLineWindow)
                    recentLineSet.Remove(recentLineQueue.Dequeue());
            }
        }
    }
}
