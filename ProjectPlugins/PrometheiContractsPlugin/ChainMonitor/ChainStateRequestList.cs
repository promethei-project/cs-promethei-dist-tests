using Logging;

namespace PrometheiContractsPlugin.ChainMonitor
{
    public class ChainStateRequestList
    {
        private readonly Lock listLock = new Lock();
        private readonly List<ChainStateRequest> requests = new();
        private readonly ILog log;
        private DateTime lastCleanup = DateTime.UtcNow;

        public ChainStateRequestList(ILog log)
        {
            this.log = new LogPrefixer(log, "(RequestList) ");
        }

        internal void Add(ChainStateRequest request)
        {
            lock (listLock)
            {
                requests.Add(request);
            }
        }

        internal void Cleanup()
        {
            if (lastCleanup > DateTime.UtcNow - TimeSpan.FromHours(1.0)) return;
            lastCleanup = DateTime.UtcNow;

            lock (listLock)
            {
                var count = requests.RemoveAll(ShouldRemove);
                log.Log($"Removed {count} finalized requests.");
            }
        }

        internal ChainStateRequest? FirstOrDefault(Func<ChainStateRequest, bool> predicate)
        {
            return requests.FirstOrDefault(predicate);
        }

        internal ChainStateRequest? SingleOrDefault(Func<ChainStateRequest, bool> predicate)
        {
            return requests.SingleOrDefault(predicate);
        }

        internal ChainStateRequest[] ToArray()
        {
            return requests.ToArray();
        }

        private bool ShouldRemove(ChainStateRequest r)
        {
            return (r.FinishedUtc + TimeSpan.FromHours(8)) < DateTime.UtcNow;
        }
    }
}
