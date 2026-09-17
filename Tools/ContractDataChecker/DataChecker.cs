using PrometheiClient;
using PrometheiContractsPlugin.ChainMonitor;
using ChainFollowingApp;
using Logging;
using Newtonsoft.Json;
using Utils;

namespace ContractDataChecker
{
    public class DataChecker : IChainFollowingHooks
    {
        private readonly LogPrefixer log;
        private readonly Configuration config;
        private readonly IPrometheiNode prometheiNode;
        private ChainState chainState = null!;
        private string lastRequestId = string.Empty;

        public DataChecker(LogPrefixer log, Configuration config, IPrometheiNode prometheiNode)
        {
            this.log = log;
            this.config = config;
            this.prometheiNode = prometheiNode;
        }

        private void CheckRandomRequestData()
        {
            if (chainState == null) return;

            var runningRequests = chainState.Requests.Where(r => r.State == PrometheiContractsPlugin.RequestState.Started).ToArray();
            if (runningRequests.Length == 0)
            {
                Log("No running requests known.");
                return;
            }
            Log($"{runningRequests.Length} running storage contracts...");
            var request = RandomUtils.GetOneRandom(runningRequests);
            if (request.Id == lastRequestId)
            {
                Log("Same request ID selected as last time. Skip.");
                return;
            }
            lastRequestId = request.Id;

            Log($"Selected running request: {request.Id}");
            Log($"Slots: {request.Ask.Slots}");
            Log($"Tolerance: {request.Ask.MaxSlotLoss}");
            Log($"Total size: {GetTotalSize(request)}");
            Log($"Finish: {Time.FormatTimestamp(request.FinishedUtc)} " +
                $"(in {Time.FormatDuration(GetTimeTillFinish(request))}) ");
            Log("Hosts:");
            foreach (var address in request.Hosts.GetHosts())
            {
                Log($" - {address}");
            }
            
            var manifest = FetchManifest(request);
            if (manifest == null)
            {
                Log("Failed to fetch manifest.");
                OutputFailure(request, "Manifest");
                return;
            }
            if (manifest.Manifest.DatasetSize.SizeInBytes > 1.GB().SizeInBytes)
            {
                Log($"Fetched manifest but dataset is too large to try download: {manifest.Manifest.DatasetSize}");
                OutputFailure(request, "TooBig");
                return;
            }

            Log($"Manifest OK:{Environment.NewLine}" +
                $"{JsonConvert.SerializeObject(manifest.Manifest, Formatting.Indented)}{Environment.NewLine}");

            if (!SuccessfulDownload(request))
            {
                Log("Failed to download.");
                OutputFailure(request, "Download");
                return;
            }
            Log("Successfully downloaded contract data.");
            OutputSuccess(request);
        }

        private ByteSize GetTotalSize(IChainStateRequest request)
        {
            return request.Ask.SlotSize.Multiply(request.Ask.Slots);
        }

        private TimeSpan GetTimeTillFinish(IChainStateRequest request)
        {
            return request.FinishedUtc - DateTime.UtcNow;
        }

        private bool SuccessfulDownload(IChainStateRequest request)
        {
            try
            {
                var result = prometheiNode.DownloadContent(request.Cid, timeout: TimeSpan.FromMinutes(30));
                if (result != null)
                {
                    try
                    {
                        File.Delete(result.Filename);
                    }
                    catch { Log("Failed to delete downloaded file!"); }
                }
                return result != null;
            }
            catch { }
            return false;
        }

        private LocalDataset? FetchManifest(IChainStateRequest request)
        {
            try
            {
                return prometheiNode.DownloadManifestOnly(request.Cid);
            }
            catch { }
            return null;
        }

        private void Log(string v)
        {
            log.Log(v);
        }

        private void OutputSuccess(IChainStateRequest request)
        {
            WriteOutput("OK", "", request);
        }

        private void OutputFailure(IChainStateRequest request, string details)
        {
            WriteOutput("FAIL", details, request);
        }

        private void WriteOutput(string conclusion, string details, IChainStateRequest request)
        {
            WriteConclusion($"{Time.FormatTimestamp(DateTime.UtcNow)}," +
                $"{conclusion},{details},{request.Id}," +
                $"{GetTotalSize(request).SizeInBytes},{GetTimeTillFinish(request).TotalSeconds}");
        }

        private readonly Lock outputLock = new Lock();
        private void WriteConclusion(string v)
        {
            lock (outputLock)
            {
                File.AppendAllLines(Path.Combine(config.DataPath, "output.log"), [v]);
            }
        }

        #region Hooks

        public void OnError(string msg)
        {
        }

        public async Task OnInitialized(ChainState chainState, int recoveredRequests)
        {
            this.chainState = chainState;
        }

        public async Task OnLoopStepFinished(bool isRealtime)
        {
            if (!isRealtime) return;

            try
            {
                CheckRandomRequestData();
            }
            catch (Exception ex)
            {
                log.Error($"{nameof(CheckRandomRequestData)} = {ex}");
            }
        }

        public async Task OnLoopStepStarting()
        {
        }

        public async Task OnRunStarting()
        {
        }

        #endregion
    }
}
