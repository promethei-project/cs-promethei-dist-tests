using Core;
using DistTestCore.Logs;
using FileUtils;
using KubernetesWorkflow;
using KubernetesWorkflow.Recipe;
using KubernetesWorkflow.Types;
using Logging;
using Utils;
using WebUtils;

namespace DistTestCore
{
    public class TestLifecycle : IK8sHooks
    {
        private const string TestsType = "dist-tests";
        private readonly EntryPoint entryPoint;
        private readonly Dictionary<string, string> metadata; 
        private readonly List<RunningPod> runningContainers = new();
        private readonly Dictionary<string, ContainerLogFollower> logFollowers = new();
        private readonly string deployId;
        private readonly List<IDownloadedLog> stoppedContainerLogs = new List<IDownloadedLog>();

        public TestLifecycle(TestLog log, Configuration configuration, IWebCallTimeSet webCallTimeSet, IK8sTimeSet k8sTimeSet, string testNamespace, string deployId, bool waitForCleanup)
        {
            Log = log;
            Configuration = configuration;
            WebCallTimeSet = webCallTimeSet;
            K8STimeSet = k8sTimeSet;
            TestNamespace = testNamespace;
            TestStartUtc = DateTime.UtcNow;

            entryPoint = new EntryPoint(log, configuration.GetK8sConfiguration(k8sTimeSet, this, testNamespace), configuration.GetFileManagerFolder(), webCallTimeSet, k8sTimeSet);
            metadata = entryPoint.GetPluginMetadata();
            CoreInterface = entryPoint.CreateInterface();
            this.deployId = deployId;
            WaitForCleanup = waitForCleanup;
            log.WriteLogTag();
        }

        public DateTime TestStartUtc { get; }
        public TestLog Log { get; }
        public Configuration Configuration { get; }
        public IWebCallTimeSet WebCallTimeSet { get; }
        public IK8sTimeSet K8STimeSet { get; }
        public string TestNamespace { get; }
        public bool WaitForCleanup { get; }
        public CoreInterface CoreInterface { get; }

        public void DeleteAllResources()
        {
            StopAllLogFollowers();
            entryPoint.Decommission(
                deleteKubernetesResources: true,
                deleteTrackedFiles: true,
                waitTillDone: WaitForCleanup
            );
        }

        public TrackedFile GenerateTestFile(ByteSize size, string label = "")
        {
            return entryPoint.Tools.GetFileManager().GenerateFile(size, label);
        }

        public TrackedFile GenerateTestFile(Action<IGenerateOption> options, string label = "")
        {
            return entryPoint.Tools.GetFileManager().GenerateFile(options, label);
        }

        public IFileManager GetFileManager()
        {
            return entryPoint.Tools.GetFileManager();
        }

        public Dictionary<string, string> GetPluginMetadata()
        {
            return entryPoint.GetPluginMetadata();
        }

        public TimeSpan GetTestDuration()
        {
            return DateTime.UtcNow - TestStartUtc;
        }

        public void OnContainersStarted(RunningPod rc)
        {
            runningContainers.Add(rc);

            foreach (var container in rc.Containers)
            {
                StartLogFollower(container);
            }
        }

        public void OnContainersStopped(RunningPod rc)
        {
            runningContainers.Remove(rc);

            stoppedContainerLogs.AddRange(rc.Containers.Select(c =>
            {
                // Prefer the follower's real-time capture: complete since
                // container start and immune to cluster log rotation. Fall
                // back to the cluster stop-log when the capture is missing
                // or unhealthy (worker died, nothing written).
                var follower = StopLogFollower(c);
                if (follower != null)
                {
                    return new DownloadedLog(follower.LogFile, c.Name);
                }
                if (c.StopLog == null) throw new Exception("Expected StopLog for stopped container " + c.Name);
                return c.StopLog;
            }));
        }

        public void OnContainerRecipeCreated(ContainerRecipe recipe)
        {
            recipe.PodLabels.Add("tests-type", TestsType);
            recipe.PodLabels.Add("deployid", deployId);
            recipe.PodLabels.Add("testid", NameUtils.GetTestId());
            recipe.PodLabels.Add("category", NameUtils.GetCategoryName());
            recipe.PodLabels.Add("fixturename", NameUtils.GetRawFixtureName());
            recipe.PodLabels.Add("testname", NameUtils.GetTestMethodName());

            foreach (var pair in metadata)
            {
                recipe.PodLabels.Add(pair.Key, pair.Value);
            }
        }

        private void StartLogFollower(RunningContainer container)
        {
            try
            {
                var follower = entryPoint.Tools.CreateWorkflow().CreateLogFollower(container);
                follower.Start();
                logFollowers.Add(container.Id, follower);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to start log follower for '{container.Name}': {ex}");
            }
        }

        // Stops the follower and returns it only when its capture is healthy
        // enough to replace the cluster stop-log: health is read before
        // stopping because IsCaptureHealthy requires a live worker.
        private ContainerLogFollower? StopLogFollower(RunningContainer container)
        {
            if (!logFollowers.Remove(container.Id, out var follower)) return null;

            var healthy = follower.IsCaptureHealthy;
            try
            {
                follower.Stop();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to stop log follower for '{container.Name}': {ex}");
            }
            return healthy ? follower : null;
        }

        private void StopAllLogFollowers()
        {
            foreach (var follower in logFollowers.Values)
            {
                try
                {
                    follower.Stop();
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to stop log follower: {ex}");
                }
            }
            logFollowers.Clear();
        }

        public IDownloadedLog[] DownloadAllLogs()
        {
            try
            {
                // TODO: This code is built on k8s containers.
                // It should be remapped to use the project plugin's support for downloading logs (via IProcessControl).
                // For now, leave this. Add support for Archivist non-container logs using the archivist node hooks.
                var result = new List<IDownloadedLog>();
                result.AddRange(stoppedContainerLogs);
                foreach (var rc in runningContainers)
                {
                    if (rc.IsStopped)
                    {
                        foreach (var c in rc.Containers)
                        {
                            if (c.StopLog == null) throw new Exception("No stop-log was downloaded for container.");
                            result.Add(c.StopLog);
                        }
                    }
                    else
                    {
                        foreach (var c in rc.Containers)
                        {
                            // Prefer the log follower's real-time capture: complete
                            // since container start and immune to cluster log rotation.
                            // Fall back to the cluster fetch only when the capture is
                            // missing or unhealthy (worker died, nothing written).
                            if (logFollowers.TryGetValue(c.Id, out var follower))
                            {
                                if (follower.IsCaptureHealthy)
                                {
                                    result.Add(new DownloadedLog(follower.LogFile, c.Name));
                                    continue;
                                }
                                Log.Log($"Log follower for '{c.Name}' is unhealthy " +
                                    $"(lines written: {follower.LinesWritten}, last write: {follower.LastWriteUtc:u}). " +
                                    "Downloading from cluster.");
                            }
                            try
                            {
                                result.Add(CoreInterface.DownloadLog(c));
                            }
                            catch (Exception e)
                            {
                                Log.Error($"Failed to download log for container '{c.Name}': {e}");
                            }
                        }
                    }
                }
                return result.ToArray();
            }
            catch (Exception ex)
            {
                Log.Error("Exception during log download: " + ex);
                return Array.Empty<IDownloadedLog>();
            }
        }
    }
}
