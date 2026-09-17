using PrometheiContractsPlugin;
using PrometheiContractsPlugin.ChainMonitor;
using GethPlugin;
using Logging;
using Utils;

namespace PrometheiReleaseTests.Utils
{
    public class ChainMonitor
    {
        private readonly ILog log;
        private readonly IGethNode gethNode;
        private readonly IPrometheiContracts contracts;
        private readonly IPeriodMonitorEventHandler periodMonitorEventHandler;
        private readonly DateTime startUtc;
        private readonly TimeSpan updateInterval;
        private readonly SlotTrackerChainStateChangeHandler slotTracker;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private Task worker = Task.CompletedTask;
        private bool monitorProofPeriods;

        public ChainMonitor(ILog log, IGethNode gethNode, IPrometheiContracts contracts, IPeriodMonitorEventHandler periodMonitorEventHandler, DateTime startUtc, TimeSpan updateInterval, bool monitorProofPeriods)
        {
            this.log = new LogPrefixer(log, "(ChainMonitor) ");
            this.gethNode = gethNode;
            this.contracts = contracts;
            this.periodMonitorEventHandler = periodMonitorEventHandler;
            this.startUtc = startUtc;
            this.updateInterval = updateInterval;
            this.monitorProofPeriods = monitorProofPeriods;
            slotTracker = new SlotTrackerChainStateChangeHandler(this.log, contracts);
        }

        public void Start(Action onFailure)
        {
            log.Log("Starting");
            cts = new CancellationTokenSource();
            worker = Task.Run(() => Worker(onFailure));
        }

        public void Stop()
        {
            log.Log("Stopping");
            Thread.Sleep(updateInterval * 3);
            cts.Cancel();
            worker.Wait();
            log.Log("Slot Report:");
            LogSlotTrackerReports();
            if (worker.Exception != null) throw worker.Exception;
        }

        public IChainStateRequest[] Requests { get; private set; } = Array.Empty<IChainStateRequest>();

        private void Worker(Action onFailure)
        {
            try
            {
                var state = new ChainState(log, gethNode, contracts, slotTracker, startUtc, monitorProofPeriods, periodMonitorEventHandler);
                Thread.Sleep(updateInterval);

                log.Log($"Chain monitoring started. Update interval: {Time.FormatDuration(updateInterval)}");
                while (!cts.IsCancellationRequested)
                {
                    state.Update();
                    Requests = state.Requests.ToArray();
                    cts.Token.WaitHandle.WaitOne(updateInterval);
                }
            }
            catch (Exception ex)
            {
                log.Error("Exception in chain monitor: " + ex);
                onFailure();
                throw;
            }
        }

        private void LogSlotTrackerReports()
        {
            slotTracker.FinalizeReports();
            var reports = slotTracker.GetSlotReports();
            foreach (var r in reports)
            {
                log.Log("");
                r.WriteToLog(log);
            }
        }
    }
}
