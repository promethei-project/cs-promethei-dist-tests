using PrometheiClient.Hooks;
using FileUtils;
using Logging;
using Utils;

namespace PrometheiClient
{
    public partial interface IPrometheiNode : IHasEthAddress, IHasMetricsScrapeTarget
    {
        string GetName();
        string GetImageName();
        string GetPeerId();
        DebugInfo GetDebugInfo(bool log = false);
        void SetLogLevel(string logLevel);
        string GetSpr();
        DebugPeer GetDebugPeer(string peerId);
        ContentId UploadFile(TrackedFile file);
        ContentId UploadFile(TrackedFile file, string contentType, string filename);
        TrackedFile? DownloadContent(ContentId contentId, string fileLabel = "");
        TrackedFile? DownloadContent(ContentId contentId, TimeSpan timeout, string fileLabel = "");
        Task<TrackedFile?> DownloadContentAsync(ContentId contentId, string fileLabel = "", CancellationToken cancellationToken = default);
        Task<TrackedFile?> DownloadContentAsync(ContentId contentId, TimeSpan timeout, string fileLabel = "", CancellationToken cancellationToken = default);
        LocalDataset DownloadStreamless(ContentId cid);
        /// <summary>
        /// TODO: This will monitor the quota-used of the node until 'size' bytes are added. That's a very bad way
        /// to track the streamless download progress. Replace it once we have a good API for this.
        /// </summary>
        LocalDataset DownloadStreamlessWait(ContentId cid, ByteSize size);
        LocalDataset DownloadManifestOnly(ContentId cid);
        LocalDatasetList LocalFiles();
        PrometheiSpace Space();
        DatasetStatus GetDatasetStatus(ContentId cid);
        void ConnectToPeer(IPrometheiNode node);
        DebugInfoVersion Version { get; }
        IMarketplaceAccess Marketplace { get; }
        ITransferSpeeds TransferSpeeds { get; }
        EthAccount EthAccount { get; }
        StoragePurchase? GetPurchaseStatus(string purchaseId);
        string[] GetPurchases();

        Address GetDiscoveryEndpoint();
        Address GetApiEndpoint();
        Address GetListenEndpoint();

        /// <summary>
        /// Warning! The node is not usable after this.
        /// TODO: Replace with delete-blocks debug call once available in Promethei.
        /// </summary>
        void DeleteDataDirFolder();
        void Stop(bool waitTillStopped);
        void InPlaceRestart();
        IDownloadedLog DownloadLog(string additionalName = "");
        bool HasCrashed();

        void SetDHTFailureProbability(int probability);
    }

    public class PrometheiNode : IPrometheiNode
    {
        private const string UploadFailedMessage = "Unable to store block";
        private static readonly TimeSpan DefaultDownloadTimeout = ParseDefaultDownloadTimeout();
        internal static TimeSpan ParseDefaultDownloadTimeout()
        {
            // Malformed or non-positive values fall back to the default so a
            // bad env var cannot break node initialization or fail downloads.
            if (int.TryParse(EnvVar.GetOrDefault("PROMETHEI_DOWNLOAD_TIMEOUT_MINUTES", "45"), out var minutes)
                && minutes > 0)
            {
                return TimeSpan.FromMinutes(minutes);
            }
            return TimeSpan.FromMinutes(45);
        }

        private readonly ILog log;
        private readonly IPrometheiNodeHooks hooks;
        private readonly TransferSpeeds transferSpeeds;
        private string peerId = string.Empty;
        private string nodeId = string.Empty;
        private readonly PrometheiAccess prometheiAccess;
        private readonly IFileManager fileManager;

        public PrometheiNode(ILog log, PrometheiAccess prometheiAccess, IFileManager fileManager, IMarketplaceAccess marketplaceAccess, IPrometheiNodeHooks hooks)
        {
            this.log = log;
            this.prometheiAccess = prometheiAccess;
            this.fileManager = fileManager;
            Marketplace = marketplaceAccess;
            this.hooks = hooks;
            Version = new DebugInfoVersion();
            transferSpeeds = new TransferSpeeds();
        }

        public void Awake()
        {
            hooks.OnNodeStarting(prometheiAccess.GetStartUtc(), prometheiAccess.GetImageName(), prometheiAccess.GetEthAccount());
        }

        public void Initialize()
        {
            // This is the moment we first connect to a promethei node. Sometimes, Kubernetes takes a while to spin up the
            // container. So we'll adding a custom, generous retry here.
            var kubeSpinupRetry = new Retry("PrometheiNode_Initialize",
                maxTimeout: TimeSpan.FromMinutes(10.0),
                sleepAfterFail: TimeSpan.FromSeconds(10.0),
                onFail: f => { },
                failFast: false);

            kubeSpinupRetry.Run(InitializePeerNodeId);

            InitializeLogReplacements();

            hooks.OnNodeStarted(this, peerId, nodeId);
        }

        public IMarketplaceAccess Marketplace { get; }
        public DebugInfoVersion Version { get; private set; }
        public ITransferSpeeds TransferSpeeds { get => transferSpeeds; }

        public StoragePurchase? GetPurchaseStatus(string purchaseId)
        {
            return prometheiAccess.GetPurchaseStatus(purchaseId);
        }

        public string[] GetPurchases()
        {
            return prometheiAccess.GetPurchases();
        }

        public EthAddress EthAddress 
        {
            get
            {
                EnsureMarketplace();
                return prometheiAccess.GetEthAccount()!.EthAddress;
            }
        }

        public EthAccount EthAccount
        {
            get
            {
                EnsureMarketplace();
                return prometheiAccess.GetEthAccount()!;
            }
        }

        public string GetName()
        {
            return prometheiAccess.GetName();
        }

        public string GetImageName()
        {
            return prometheiAccess.GetImageName();
        }

        public string GetPeerId()
        {
            return peerId;
        }

        public DebugInfo GetDebugInfo(bool log = false)
        {
            var debugInfo = prometheiAccess.GetDebugInfo();
            if (log)
            {
                var known = string.Join(",", debugInfo.Table.Nodes.Select(n => n.PeerId));
                Log($"Got DebugInfo with id: {debugInfo.Id}. This node knows: [{known}]");
            }
            return debugInfo;
        }

        public void SetLogLevel(string logLevel)
        {
            prometheiAccess.SetLogLevel(logLevel);
        }

        public string GetSpr()
        {
            return prometheiAccess.GetSpr();
        }

        public DebugPeer GetDebugPeer(string peerId)
        {
            return prometheiAccess.GetDebugPeer(peerId);
        }

        public ContentId UploadFile(TrackedFile file)
        {
            return UploadFile(file, "application/octet-stream", Path.GetFileName(file.Filename));
        }

        public ContentId UploadFile(TrackedFile file, string contentType, string filename)
        {
            using var fileStream = File.OpenRead(file.Filename);
            var uniqueId = Guid.NewGuid().ToString();
            var size = file.GetFilesize();

            hooks.OnFileUploading(uniqueId, size);

            var contentDisposition = $"attachment; filename=\"{filename}\"";
            var input = new UploadInput(contentType, contentDisposition, fileStream);
            var logMessage = $"Uploading file {file.Describe()} with contentType: '{input.ContentType}' and disposition: '{input.ContentDisposition}'...";
            var measurement = Stopwatch.Measure(log, logMessage, () =>
            {
                return prometheiAccess.UploadFile(input);
            });

            var response = measurement.Value;
            transferSpeeds.AddUploadSample(size, measurement.Duration);

            if (string.IsNullOrEmpty(response)) FrameworkAssert.Fail("Received empty response.");
            if (response.StartsWith(UploadFailedMessage)) FrameworkAssert.Fail("Node failed to store block.");

            Log($"Uploaded file {file.Describe()}. Received contentId: '{response}'.");

            var cid = new ContentId(response, size);
            hooks.OnFileUploaded(uniqueId, size, cid);
            return cid;
        }

        public TrackedFile? DownloadContent(ContentId contentId, string fileLabel = "")
        {
            return DownloadContent(contentId, DefaultDownloadTimeout, fileLabel);
        }

        public TrackedFile? DownloadContent(ContentId contentId, TimeSpan timeout, string fileLabel = "")
        {
            var file = fileManager.CreateEmptyFile(fileLabel);
            hooks.OnFileDownloading(contentId);
            Log($"Downloading '{contentId}'...");

            var logMessage = $"Downloaded '{contentId}' to '{file.Filename}'";
            var measurement = Stopwatch.Measure(log, logMessage, () => DownloadToFile(contentId.Id, file, timeout));

            var size = file.GetFilesize();
            transferSpeeds.AddDownloadSample(size, measurement);
            hooks.OnFileDownloaded(size, contentId);

            return file;
        }

        public Task<TrackedFile?> DownloadContentAsync(ContentId contentId, string fileLabel = "", CancellationToken cancellationToken = default)
        {
            return DownloadContentAsync(contentId, DefaultDownloadTimeout, fileLabel, cancellationToken);
        }

        public async Task<TrackedFile?> DownloadContentAsync(ContentId contentId, TimeSpan timeout, string fileLabel = "", CancellationToken cancellationToken = default)
        {
            var file = fileManager.CreateEmptyFile(fileLabel);
            hooks.OnFileDownloading(contentId);
            Log($"Downloading '{contentId}'...");

            var sw = Stopwatch.Begin(log, $"Downloaded '{contentId}' to '{file.Filename}'");

            await DownloadToFileAsync(contentId.Id, file, timeout, cancellationToken);

            var duration = sw.End();
            var size = file.GetFilesize();
            transferSpeeds.AddDownloadSample(size, duration);
            hooks.OnFileDownloaded(size, contentId);

            return file;
        }

        public LocalDataset DownloadStreamless(ContentId cid)
        {
            Log($"Downloading streamless '{cid}' (no-wait)");
            return prometheiAccess.DownloadStreamless(cid);
        }

        public LocalDataset DownloadStreamlessWait(ContentId cid, ByteSize size)
        {
            Log($"Downloading streamless '{cid}' (wait till finished)");

            var sw = Stopwatch.Measure(log, nameof(DownloadStreamlessWait), () =>
            {
                var startSpace = Space();
                var result = prometheiAccess.DownloadStreamless(cid);
                WaitUntilQuotaUsedIncreased(startSpace, size);
                return result;
            });

            return sw.Value;
        }

        public LocalDataset DownloadManifestOnly(ContentId cid)
        {
            Log($"Downloading manifest-only '{cid}'");
            return prometheiAccess.DownloadManifestOnly(cid);
        }

        public LocalDatasetList LocalFiles()
        {
            var files = prometheiAccess.LocalFiles();
            Log($"Files: {files}");
            return files;
        }

        public PrometheiSpace Space()
        {
            var space = prometheiAccess.Space();
            Log($"Space: {space}");
            return space;
        }

        public DatasetStatus GetDatasetStatus(ContentId cid)
        {
            var status = prometheiAccess.GetDatasetStatus(cid);
            Log($"DatasetStatus: {status}");
            return status;
        }

        public void ConnectToPeer(IPrometheiNode node)
        {
            var peer = (PrometheiNode)node;

            Log($"Connecting to peer {peer.GetName()}...");
            var peerInfo = node.GetDebugInfo();
            prometheiAccess.ConnectToPeer(peerInfo.Id, GetPeerMultiAddresses(peer, peerInfo));

            Log($"Successfully connected to peer {peer.GetName()}.");
        }

        public void DeleteDataDirFolder()
        {
            prometheiAccess.DeleteDataDirFolder();
        }

        public void Stop(bool waitTillStopped)
        {
            Log("Stopping...");
            hooks.OnNodeStopping();
            prometheiAccess.Stop(waitTillStopped);
        }

        public void InPlaceRestart()
        {
            Log("In-place restart...");
            hooks.OnNodeRestarting();
            prometheiAccess.Restart();

            Time.WaitUntil(() =>
            {
                try
                {
                    return !string.IsNullOrEmpty(GetDebugInfo().Id);
                }
                catch
                {
                    return false;
                }
            }, nameof(InPlaceRestart));

            hooks.OnNodeRestarted();
        }

        public IDownloadedLog DownloadLog(string additionalName = "")
        {
            return prometheiAccess.DownloadLog(additionalName);
        }

        public Address GetDiscoveryEndpoint()
        {
            return prometheiAccess.GetDiscoveryEndpoint();
        }

        public Address GetApiEndpoint()
        {
            return prometheiAccess.GetApiEndpoint();
        }

        public Address GetListenEndpoint()
        {
            return prometheiAccess.GetListenEndpoint();
        }

        public Address GetMetricsScrapeTarget()
        {
            var address = prometheiAccess.GetMetricsEndpoint();
            if (address == null) throw new Exception("Metrics ScrapeTarget accessed, but node was not started with EnableMetrics()");
            return address;
        }

        public bool HasCrashed()
        {
            return prometheiAccess.HasCrashed();
        }

        public void SetDHTFailureProbability(int probability)
        {
            if (probability < 0) throw new ArgumentException(nameof(probability));

            prometheiAccess.SetSystemTestingOption(
                "dht_send_fail_probability",
                probability.ToString()
            );
        }

        public override string ToString()
        {
            return $"PrometheiNode:{GetName()}";
        }

        private void InitializePeerNodeId()
        {
            var debugInfo = prometheiAccess.GetDebugInfo();
            if (!debugInfo.Version.IsValid())
            {
                throw new Exception($"Invalid version information received from Promethei node {GetName()}: {debugInfo.Version}");
            }

            peerId = debugInfo.Id;
            nodeId = debugInfo.Table.LocalNode.NodeId;
            Version = debugInfo.Version;
        }

        private void InitializeLogReplacements()
        {
            var nodeName = GetName();

            log.AddStringReplace(peerId, nodeName);
            log.AddStringReplace(PrometheiUtils.ToShortId(peerId), nodeName);
            log.AddStringReplace(nodeId, nodeName);
            log.AddStringReplace(PrometheiUtils.ToShortId(nodeId), nodeName);

            var ethAccount = prometheiAccess.GetEthAccount();
            if (ethAccount != null)
            {
                var addr = ethAccount.EthAddress.ToString();
                log.AddStringReplace(addr, nodeName);
            }
        }

        private string[] GetPeerMultiAddresses(PrometheiNode peer, DebugInfo peerInfo)
        {
            var peerId = peer.GetDiscoveryEndpoint().Host
                .Replace("http://", "")
                .Replace("https://", "");

            return peerInfo.Addrs.Select(a => a
                .Replace("0.0.0.0", peerId))
                .ToArray();
        }

        private void DownloadToFile(string contentId, TrackedFile file, TimeSpan timeout)
        {
            // Delegate to the cancellation-aware async path: the sync download
            // API cannot interrupt a blocking CopyTo, so a timed-out transfer
            // would keep running on the stream after disposal. The async path
            // cancels the HTTP work itself and throws the same TimeoutException.
            DownloadToFileAsync(contentId, file, timeout, CancellationToken.None).GetAwaiter().GetResult();
        }

        private async Task DownloadToFileAsync(string contentId, TrackedFile file, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await using var fileStream = new FileStream(
                file.Filename, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 1024 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            try
            {
                await prometheiAccess.DownloadFileAsync(contentId, fileStream, linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Download of '{contentId}' timed out after {Time.FormatDuration(timeout)}");
            }
            catch (Exception ex)
            {
                Log($"Failed to download file '{contentId}': {ex}");
                throw;
            }
        }

        public void WaitUntilQuotaUsedIncreased(PrometheiSpace startSpace, ByteSize expectedIncreaseOfQuotaUsed)
        {
            WaitUntilQuotaUsedIncreased(startSpace, expectedIncreaseOfQuotaUsed, TimeSpan.FromMinutes(30));
        }

        public void WaitUntilQuotaUsedIncreased(
            PrometheiSpace startSpace,
            ByteSize expectedIncreaseOfQuotaUsed,
            TimeSpan maxTimeout)
        {
            Log($"Waiting until quotaUsed " +
                $"(start: {startSpace.QuotaUsedBytes}) " +
                $"increases by {expectedIncreaseOfQuotaUsed} " +
                $"to reach {startSpace.QuotaUsedBytes + expectedIncreaseOfQuotaUsed.SizeInBytes}");

            var retry = new Retry($"Checking local space for quotaUsed increase of {expectedIncreaseOfQuotaUsed}",
            maxTimeout: maxTimeout,
            sleepAfterFail: TimeSpan.FromSeconds(10),
            onFail: f => { },
            failFast: false);

            retry.Run(() =>
            {
                var space = Space();
                var increase = space.QuotaUsedBytes - startSpace.QuotaUsedBytes;

                if (increase < expectedIncreaseOfQuotaUsed.SizeInBytes)
                    throw new Exception($"Expected quota-used not reached. " +
                        $"Expected increase: {expectedIncreaseOfQuotaUsed.SizeInBytes} " +
                        $"Actual increase: {increase} " +
                        $"Actual used: {space.QuotaUsedBytes}");
            });
        }

        private void EnsureMarketplace()
        {
            if (prometheiAccess.GetEthAccount() == null) throw new Exception("Marketplace is not enabled for this Promethei node. Please start it with the option '.EnableMarketplace(...)' to enable it.");
        }

        private void Log(string msg)
        {
            log.Log(msg);
        }
    }
}
