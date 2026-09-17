using System.Numerics;
using BlockchainUtils;
using Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Utils;

namespace PrometheiContractsPlugin.ChainMonitor
{
    public class SlotTrackerChainStateChangeHandler : IChainStateChangeHandler
    {
        private readonly string[] requestIdsToTrack;
        private readonly Dictionary<string, Dictionary<ulong, SlotReport>> reports = new Dictionary<string, Dictionary<ulong, SlotReport>>();
        private readonly ILog log;
        private readonly IPrometheiContracts contracts;

        public SlotTrackerChainStateChangeHandler(ILog log, IPrometheiContracts contracts, params string[] requestIdsToTrack)
        {
            this.requestIdsToTrack = requestIdsToTrack.Select(r => r.ToLowerInvariant()).ToArray();
            this.log = log;
            this.contracts = contracts;
        }

        public void FinalizeReports()
        {
            FindSlotReserveCalls(DateTime.UtcNow);
        }

        public SlotReport[] GetSlotReports()
        {
            return reports.SelectMany(p => p.Value.Select(e => e.Value)).ToArray();
        }

        public void OnNewRequest(RequestEvent requestEvent)
        {
            if (!IsMyRequest(requestEvent)) return;
            GetMap(requestEvent);
        }

        public void OnRequestCancelled(RequestEvent requestEvent)
        {
            if (!IsMyRequest(requestEvent)) return;
            var d = GetMap(requestEvent);
            foreach (var pair in d) pair.Value.RequestCancelled(requestEvent.Block);
        }

        public void OnRequestFailed(RequestEvent requestEvent)
        {
            if (!IsMyRequest(requestEvent)) return;
            var d = GetMap(requestEvent);
            foreach (var pair in d) pair.Value.RequestFailed(requestEvent.Block);
        }

        public void OnRequestFinished(RequestEvent requestEvent, IChainStateRequest? extendedBy)
        {
            if (!IsMyRequest(requestEvent)) return;
            var d = GetMap(requestEvent);
            foreach (var pair in d) pair.Value.RequestFinished(requestEvent.Block);
        }

        public void OnRequestFulfilled(RequestEvent requestEvent)
        {
            if (!IsMyRequest(requestEvent)) return;
            var d = GetMap(requestEvent);
            foreach (var pair in d) pair.Value.RequestStarted(requestEvent.Block);
        }

        public void OnSlotFilled(RequestEvent requestEvent, EthAddress host, BigInteger slotIndex, bool isRepair)
        {
            if (!IsMyRequest(requestEvent)) return;
            var d = GetMap(requestEvent);
            d[(ulong)slotIndex].SlotFilled(host, isRepair, requestEvent.Block);
        }

        public void OnSlotFreed(RequestEvent requestEvent, BigInteger slotIndex)
        {
            if (!IsMyRequest(requestEvent)) return;
            var d = GetMap(requestEvent);
            d[(ulong)slotIndex].SlotFreed(requestEvent.Block);
        }

        public void OnSlotReservationsFull(RequestEvent requestEvent, BigInteger slotIndex)
        {
            if (!IsMyRequest(requestEvent)) return;
            var d = GetMap(requestEvent);
            d[(ulong)slotIndex].SlotReservationsFull(requestEvent.Block);
        }

        public void OnError(string msg)
        {
        }

        public void OnProofSubmitted(BlockTimeEntry block, string id)
        {
        }

        private Dictionary<ulong, SlotReport> GetMap(RequestEvent requestEvent)
        {
            if (reports.TryGetValue(requestEvent.Request.Id, out var map))
            {
                return map;
            }

            var d = new Dictionary<ulong, SlotReport>();
            for (ulong s = 0; s < requestEvent.Request.Ask.Slots; s++)
            {
                d.Add(s, new SlotReport(requestEvent.Request.Id, s, requestEvent.Block));
            }
            reports.Add(requestEvent.Request.Id, d);
            return d;
        }

        private void FindSlotReserveCalls(DateTime endUtc)
        {
            if (reports.Count == 0)
            {
                Log("No requests in slotTracker.");
                return;
            }

            var earliestCreationUtc = FindEarliestCreationUtc();
            var timeInterval = new TimeRange(earliestCreationUtc, endUtc);
            Log($"Collecting ReserveSlot calls in range {timeInterval}");
            var events = contracts.GetEvents(timeInterval);
            events.GetReserveSlotCalls(call =>
            {
                var requestId = call.RequestId.ToHex().ToLowerInvariant();
                if (reports.TryGetValue(requestId, out var map))
                {
                    map[call.SlotIndex].SlotReserved(call.FromAddress, call.Block);
                }
            });
        }

        private DateTime FindEarliestCreationUtc()
        {
            var result = DateTime.MaxValue;
            foreach (var pair in reports)
            {
                var creationUtc = pair.Value.First().Value.CreationBlock.Utc;
                if (creationUtc < result) result = creationUtc;
            }
            return result;
        }

        private bool IsMyRequest(RequestEvent requestEvent)
        {
            if (requestIdsToTrack.Length == 0) return true;
            return requestIdsToTrack.Any(id => id == requestEvent.Request.Id);
        }

        private void Log(string msg)
        {
            log.Log(msg);
        }
    }

    public class SlotReport
    {
        private readonly List<(BlockTimeEntry, string)> entries = new List<(BlockTimeEntry, string)>();

        public SlotReport(string id, ulong s, BlockTimeEntry block)
        {
            RequestId = id;
            SlotIndex = s;
            CreationBlock = block;

            entries.Add((block, "Request created"));
        }

        public string RequestId { get; }
        public ulong SlotIndex { get; }
        public BlockTimeEntry CreationBlock { get; }

        public (BlockTimeEntry, string)[] GetSorted()
        {
            return entries.OrderBy(e => e.Item1.Utc).ToArray();
        }

        public void WriteToLog(ILog log)
        {
            var entries = GetSorted();
            var prefix = $"(request:{RequestId} slotIndex:{SlotIndex})";
            foreach (var (blk, entry) in entries)
            {
                log.Log($"{blk}{prefix} {entry}");
            }
        }

        public void RequestCancelled(BlockTimeEntry block)
        {
            entries.Add((block, "Request cancelled"));
        }

        public void RequestFailed(BlockTimeEntry block)
        {
            entries.Add((block, "Request failed"));
        }

        public void RequestFinished(BlockTimeEntry block)
        {
            entries.Add((block, "Request finished"));
        }

        public void RequestStarted(BlockTimeEntry block)
        {
            entries.Add((block, "Request started"));
        }

        public void SlotFilled(EthAddress host, bool isRepair, BlockTimeEntry block)
        {
            entries.Add((block, $"Slot filled by {host}" + (isRepair ? " (repair)" : "")));
        }

        public void SlotFreed(BlockTimeEntry block)
        {
            entries.Add((block, "Slot freed"));
        }

        public void SlotReservationsFull(BlockTimeEntry block)
        {
            entries.Add((block, "Slot reservations full"));
        }

        public void SlotReserved(string host, BlockTimeEntry block)
        {
            entries.Add((block, $"Slot reserved by {host}"));
        }
    }
}
