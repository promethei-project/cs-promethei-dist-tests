using PrometheiContractsPlugin;
using PrometheiContractsPlugin.ChainMonitor;
using BlockchainUtils;
using GethPlugin;
using Logging;
using Utils;

namespace TraceContract
{
    public class ChainTracer
    {
        private readonly ILog log;
        private readonly ILog baseLog;
        private readonly IGethNode geth;
        private readonly IPrometheiContracts contracts;
        private readonly Input input;
        private readonly Output output;

        public ChainTracer(ILog log, ILog baseLog, IGethNode geth, IPrometheiContracts contracts, Input input, Output output)
        {
            this.log = log;
            this.baseLog = baseLog;
            this.geth = geth;
            this.contracts = contracts;
            this.input = input;
            this.output = output;
        }

        public TimeRange TraceChainTimeline()
        {
            log.Log("Querying blockchain...");
            var request = Measure(GetRequest, nameof(GetRequest));
            if (request == null) throw new Exception("Failed to find the purchase in the last week of transactions.");

            var startUtc = CalculateStartUtc(request);

            log.Log($"Request started at {Time.FormatTimestamp(startUtc)}");
            output.LogRequest(startUtc, input.RequestId, request.Request);
            var contractEnd = Measure(() => RunToContractEnd(startUtc), nameof(RunToContractEnd));

            log.Log($"Request timeline: {startUtc} -> {contractEnd}");

            // For this timeline, we log all the calls to reserve-slot.
            var startBlock = geth.GetLowestBlockAfterUtc(startUtc);
            var blockRange = new BlockInterval(
                new TimeRange(startBlock.Utc, contractEnd.Utc),
                startBlock.BlockNumber, contractEnd.BlockNumber);

            log.Log("Writing blockchain output...");
            output.WriteContractEvents();

            return blockRange.TimeRange;
        }

        private DateTime CalculateStartUtc(CacheRequest request)
        {
            ulong toleranceSeconds = 30;
            return request.ExpiryUtc - TimeSpan.FromSeconds(request.Request.Expiry + toleranceSeconds);
        }

        private T Measure<T>(Func<T> task, string name)
        {
            return Stopwatch.Measure(log, name, task).Value;
        }

        private BlockTimeEntry RunToContractEnd(DateTime utc)
        {
            var tracker = new ChainRequestTracker(output, input.PurchaseId);
            var slotTracker = new SlotTrackerChainStateChangeHandler(log, contracts, input.PurchaseId);
            output.AddSlotTracker(slotTracker);
            var mux = new ChainStateChangeHandlerMux(tracker, slotTracker);
            var chainState = new ChainState(baseLog, geth, contracts, mux, utc, false, new DoNothingPeriodMonitorEventHandler(false));

            while (!tracker.IsFinished)
            {
                utc += TimeSpan.FromMinutes(10.0);
                if (utc > DateTime.UtcNow)
                {
                    log.Log("Caught up to present moment without finding contract end.");
                    utc = DateTime.UtcNow;
                    log.Log($"Querying up to {Time.FormatTimestamp(utc)}");
                    chainState.Update(utc);
                    return geth.GetHighestBlockBeforeUtc(utc)!;
                }

                log.Log($"Querying up to {Time.FormatTimestamp(utc)}");
                chainState.Update(utc);
            }

            return tracker.FinishBlock!;
        }

        private CacheRequest? GetRequest()
        {
            return contracts.GetRequest(input.RequestId);
        }
    }
}
