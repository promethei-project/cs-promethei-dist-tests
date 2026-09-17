using PrometheiContractsPlugin.Marketplace;
using BlockchainUtils;
using GethPlugin;
using Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Utils;

namespace PrometheiContractsPlugin.ChainMonitor
{
    public interface IPeriodMonitorEventHandler
    {
        void OnPeriodReport(PeriodReport report);
        bool GetLogPeriodReports();
    }

    public class PeriodMonitor
    {
        private readonly ILog log;
        private readonly IPrometheiContracts contracts;
        private readonly IGethNode geth;
        private readonly IPeriodMonitorEventHandler eventHandler;
        private readonly List<PeriodReport> reports = new List<PeriodReport>();
        private CurrentPeriod? currentPeriod = null;
        private BlockTimeEntry? lastUpdate = null;

        public PeriodMonitor(ILog log, IPrometheiContracts contracts, IGethNode geth, IPeriodMonitorEventHandler eventHandler)
        {
            this.log = new LogPrefixer(log, "(PeriodMonitor) ");
            this.contracts = contracts;
            this.geth = geth;
            this.eventHandler = eventHandler;
        }

        public void Update(BlockTimeEntry block, IChainStateRequest[] requests)
        {
            if (lastUpdate != null)
            {
                if (block.BlockNumber != lastUpdate.BlockNumber + 1)
                {
                    throw new Exception($"Discontinuous update called on PeriodMonitor. lastUpdate: {lastUpdate} call: {block}");
                }
            }
            log.Debug($"Updating for block {block}...");
            lastUpdate = block;

            var updateToPeriodNumber = contracts.GetPeriodNumber(block.Utc);
            if (currentPeriod == null)
            {
                currentPeriod = CreateCurrentPeriod(block, updateToPeriodNumber, requests);
                return;
            }
            if (updateToPeriodNumber == currentPeriod.PeriodNumber) return;

            // the previous block is the last block in currentPeriod.
            var closingBlock = geth.GetBlockForNumber(block.BlockNumber - 1);
            if (closingBlock == null) throw new Exception("Unable to find period-closing block.");
            CreateReportForPeriod(closingBlock, currentPeriod, requests);
            currentPeriod = CreateCurrentPeriod(block, updateToPeriodNumber, requests);
        }

        public PeriodMonitorResult GetAndClearReports()
        {
            var result = reports.ToArray();
            reports.Clear();
            return new PeriodMonitorResult(result);
        }

        private CurrentPeriod CreateCurrentPeriod(BlockTimeEntry block, ulong periodNumber, IChainStateRequest[] requests)
        {
            var cRequests = requests.Select(r => CurrentRequest.CreateCurrentRequest(contracts, r)).ToArray();
            log.Log($"New period {periodNumber} started with block {block}");
            return new CurrentPeriod(block, periodNumber, cRequests);
        }

        private void CreateReportForPeriod(BlockTimeEntry closingBlock, CurrentPeriod currentPeriod, IChainStateRequest[] requests)
        {
            log.Debug($"Creating report for period {currentPeriod.PeriodNumber} with closing block {closingBlock}...");

            // Fetch function calls during period. Format report.
            var timeRange = contracts.GetPeriodTimeRange(currentPeriod.PeriodNumber);
            var blockRange = new BlockInterval(timeRange, currentPeriod.StartingBlock.BlockNumber, closingBlock.BlockNumber);

            var (callReports, markMissingCalls) = FetchCallReports(blockRange);

            var requestReports = new List<PeriodRequestReport>();
            var currentRemaining = currentPeriod.CurrentRequests.ToList();
            foreach (var r in requests)
            {
                var current = TakeMatchingCurrent(currentRemaining, r);
                if (current == null)
                {
                    // Request is new during this period.
                    requestReports.Add(PeriodRequestReport.CreatePeriodRequestReport(contracts, r));
                }
                else
                {
                    // Request existed at start of period.
                    var hasEnded = r.State == RequestState.Cancelled || r.State == RequestState.Finished || r.State == RequestState.Failed;
                    requestReports.Add(PeriodRequestReport.CreatePeriodRequestReport(
                        contracts,
                        currentPeriod.PeriodNumber,
                        current,
                        hasEnded,
                        markMissingCalls));
                }
            }
            // If there remain currentRequests, those seem to have vanished from the chainstate
            // (That's odd. This shouldn't happen. We should log this as a warning.)
            // We'll consider them ended during this period.
            foreach (var remaining in currentRemaining)
            {
                log.Log($"Warning: A request disappeared from the ChainState. This shouldn't happen. requestId:{remaining.Request.RequestId.ToHex()}");
                requestReports.Add(PeriodRequestReport.CreatePeriodRequestReport(
                        contracts,
                        currentPeriod.PeriodNumber,
                        remaining,
                        hasEnded: true,
                        markMissingCalls));
            }

            var report = new PeriodReport(
                new ProofPeriod(currentPeriod.PeriodNumber, timeRange, blockRange),
                requestReports.ToArray(),
                callReports.ToArray());

            if (eventHandler.GetLogPeriodReports())
            {
                report.Log(log);
            }
            reports.Add(report);

            eventHandler.OnPeriodReport(report);
        }

        private CurrentRequest? TakeMatchingCurrent(List<CurrentRequest> currentRemaining, IChainStateRequest r)
        {
            var current = currentRemaining.ToArray();
            foreach (var c in current)
            {
                if (c.Request.RequestId.ToHex() == r.RequestId.ToHex())
                {
                    currentRemaining.Remove(c);
                    return c;
                }
            }
            return null;
        }

        private (FunctionCallReport[], MarkProofAsMissingFunction[]) FetchCallReports(BlockInterval blockRange)
        {
            var callReports = new List<FunctionCallReport>();
            var missedCalls = new List<MarkProofAsMissingFunction>();

            var events = contracts.GetEvents(blockRange);
            var reporter = new CallReporter(callReports, events);
            reporter.Run(missedCalls.Add);

            return (callReports.ToArray(), missedCalls.ToArray());
        }
    }

    public class DoNothingPeriodMonitorEventHandler : IPeriodMonitorEventHandler
    {
        private readonly bool logPeriodReports;

        public DoNothingPeriodMonitorEventHandler(bool logPeriodReports)
        {
            this.logPeriodReports = logPeriodReports;
        }

        public bool GetLogPeriodReports()
        {
            return logPeriodReports;
        }

        public void OnPeriodReport(PeriodReport report)
        {
        }
    }
}
