using PrometheiContractsPlugin.ChainMonitor;

namespace TestNetRewarder
{
    public class PeriodMonitorHandler : IPeriodMonitorEventHandler
    {
        private readonly EventsFormatter eventsFormatter;

        public PeriodMonitorHandler(EventsFormatter eventsFormatter)
        {
            this.eventsFormatter = eventsFormatter;
        }

        public bool GetLogPeriodReports()
        {
            return false;
        }

        public void OnPeriodReport(PeriodReport report)
        {
            var missedSlots = new List<MissedSlot>();

            foreach (var r in report.Requests)
            {
                foreach (var s in r.Slots)
                {
                    if (s.GetIsProofMissed())
                    {
                        missedSlots.Add(new MissedSlot(r.Request, s));
                    }
                }
            }

            eventsFormatter.OnPeriodReport(new PeriodReportWithMisses(report, missedSlots.ToArray()));
        }
    }

    public class PeriodReportWithMisses
    {
        public PeriodReportWithMisses(PeriodReport periodReport, MissedSlot[] missedSlots)
        {
            PeriodReport = periodReport;
            MissedSlots = missedSlots;
        }

        public PeriodReport PeriodReport { get; }
        public MissedSlot[] MissedSlots { get; }
    }

    public class MissedSlot
    {
        public MissedSlot(IChainStateRequest request, PeriodRequestSlotReport slotReport)
        {
            Request = request;
            SlotReport = slotReport;
        }

        public IChainStateRequest Request { get; }
        public PeriodRequestSlotReport SlotReport { get; }
    }
}
