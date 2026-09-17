using PrometheiContractsPlugin;
using PrometheiContractsPlugin.ChainMonitor;
using Logging;
using Utils;

namespace ChainFollowingApp
{
    public class Processor : ITimeSegmentHandler
    {
        private readonly ChainState chainState;
        private readonly ILog log;
        private readonly ChainFollowConfig config;
        private readonly ChainFollowHandlers handlers;

        public Processor(ILog log, ChainFollowConfig config, ChainFollowHandlers handlers)
        {
            this.log = log;
            this.config = config;
            this.handlers = handlers;
            chainState = new ChainState(log, config.RpcNode, config.Contracts, handlers.ChainStateHandler, config.HistoryStartUtc,
                doProofPeriodMonitoring: handlers.ProofMonitorHandler != null, handlers.ProofMonitorHandler!);
        }

        public async Task Initialize()
        {
            var numRecoveredRequests = TryRecoverRunningRequests(config.RequestsCache);
            await handlers.Hooks.OnInitialized(chainState, numRecoveredRequests);
        }

        public async Task<TimeSegmentResponse> OnNewSegment(TimeRange timeRange)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var numberOfChainEvents = await ProcessEvents(timeRange);
                var duration = sw.Elapsed;

                log.Log($"{nameof(ProcessEvents)} = {Time.FormatDuration(duration)}");
                if (duration < TimeSpan.FromSeconds(1)) return TimeSegmentResponse.Underload;
                if (duration > TimeSpan.FromSeconds(3)) return TimeSegmentResponse.Overload;
                return TimeSegmentResponse.OK;
            }
            catch (Exception ex)
            {
                var msg = "Exception processing time segment: " + ex;
                log.Error(msg);
                handlers.Hooks.OnError(msg);
                throw;
            }
        }

        private int TryRecoverRunningRequests(IRequestsCache requestsCache)
        {
            var recovered = 0;
            requestsCache.IterateAll(requestId =>
            {
                var r = false;
                try
                {
                    r = chainState.TryRecoverRunningRequest(requestId);
                }
                catch
                {
                    // TryRecoverRunningRequest returns true when the id is found
                    // and the request is still relevant.
                    // It returns false when it is found and no longer relevant.
                    // And it throws if the call fails.
                    // In that case, we don't delete the requestId. We can retry it later.
                }

                if (r)
                {
                    recovered++;
                }
                else
                {
                    requestsCache.Delete(requestId);
                }
            });
            log.Log("Recovered requests: " + recovered);
            return recovered;
        }

        private async Task<int> ProcessEvents(TimeRange timeRange)
        {
            log.Log($"Processing time range: {timeRange}");
            try
            {
                return chainState.Update(timeRange.To);
            }
            catch (Exception ex)
            {
                log.Error($"Failed to update chainState for time range {timeRange}: {ex}");
                return 0;
            }
        }
    }
}
