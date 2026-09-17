using PrometheiContractsPlugin;
using PrometheiContractsPlugin.ChainMonitor;
using GethPlugin;
using Logging;

namespace ChainFollowingApp
{
    public interface IChainFollowingHooks
    {
        Task OnInitialized(ChainState chainState, int recoveredRequests);
        Task OnRunStarting();
        Task OnLoopStepStarting();
        Task OnLoopStepFinished(bool isRealtime);
        void OnError(string msg);
    }

    public class ChainFollowConfig
    {
        public ChainFollowConfig(ILog log, TimeSpan updateInterval, DateTime historyStartUtc, IGethNode rpcNode, IPrometheiContracts contracts, IRequestsCache requestsCache)
        {
            Log = log;
            UpdateInterval = updateInterval;
            HistoryStartUtc = historyStartUtc;
            RpcNode = rpcNode;
            Contracts = contracts;
            RequestsCache = requestsCache;
        }

        public ILog Log { get; }
        public TimeSpan UpdateInterval { get; }
        public DateTime HistoryStartUtc { get; }
        public IGethNode RpcNode { get; }
        public IPrometheiContracts Contracts { get; }
        public IRequestsCache RequestsCache { get; }
    }

    public class ChainFollowHandlers
    {
        public ChainFollowHandlers(IChainFollowingHooks hooks, IChainStateChangeHandler chainStateHandler, IPeriodMonitorEventHandler? proofMonitorHandler)
        {
            Hooks = hooks;
            ChainStateHandler = chainStateHandler;
            ProofMonitorHandler = proofMonitorHandler;
        }

        public IChainFollowingHooks Hooks { get; }
        public IChainStateChangeHandler ChainStateHandler { get; }
        public IPeriodMonitorEventHandler? ProofMonitorHandler { get; }
    }

    public class ChainFollowing
    {
        private readonly ILog log;
        private readonly ChainFollowConfig config;
        private readonly ChainFollowHandlers handlers;

        public ChainFollowing(ChainFollowConfig config, ChainFollowHandlers handlers)
        {
            log = new LogPrefixer(config.Log, "(ChainFollow)");
            this.config = config;
            this.handlers = handlers;
        }
       
        public async Task Run(CancellationToken ct)
        {
            EnsureRPCOnline();

            log.Log("Initializing...");
            var processor = new Processor(log, config, handlers);
            await processor.Initialize();

            log.Log("Starting...");
            var segmenter = new TimeSegmenter(log, config.UpdateInterval, config.HistoryStartUtc, processor);
            await handlers.Hooks.OnRunStarting();

            log.Log("Running...");
            while (!ct.IsCancellationRequested)
            {
                await handlers.Hooks.OnLoopStepStarting();
                await segmenter.ProcessNextSegment(ct);
                await handlers.Hooks.OnLoopStepFinished(segmenter.IsRealtime);
                await Task.Delay(100, ct);
            }
        }

        private void EnsureRPCOnline()
        {
            log.Log("Checking Eth RPC...");
            var blockNumber = config.RpcNode.GetSyncedBlockNumber();
            if (blockNumber == null || blockNumber < 1) throw new Exception("Eth RPC connection failed.");
            log.Log("Eth RPC OK. Block number: " + blockNumber);
        }
    }
}
