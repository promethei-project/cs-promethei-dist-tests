using PrometheiClient;
using PrometheiContractsPlugin.Marketplace;
using BlockchainUtils;
using GethPlugin;
using Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;
using Utils;

namespace PrometheiContractsPlugin.ChainMonitor
{
    public interface IChainStateChangeHandler
    {
        void OnNewRequest(RequestEvent requestEvent);
        void OnRequestFinished(RequestEvent requestEvent, IChainStateRequest? extendedBy);
        void OnRequestFulfilled(RequestEvent requestEvent);
        void OnRequestCancelled(RequestEvent requestEvent);
        void OnRequestFailed(RequestEvent requestEvent);
        void OnSlotFilled(RequestEvent requestEvent, EthAddress host, BigInteger slotIndex, bool isRepair);
        void OnSlotFreed(RequestEvent requestEvent, BigInteger slotIndex);
        void OnSlotReservationsFull(RequestEvent requestEvent, BigInteger slotIndex);
        void OnProofSubmitted(BlockTimeEntry block, string id);
        void OnError(string msg);
    }

    public class RequestEvent
    {
        public RequestEvent(BlockTimeEntry block, IChainStateRequest request)
        {
            Block = block;
            Request = request;
        }

        public BlockTimeEntry Block { get; }
        public IChainStateRequest Request { get; }
    }

    public class ChainState
    {
        private readonly ChainStateRequestList requests;
        private readonly ILog log;
        private readonly IGethNode geth;
        private readonly IPrometheiContracts contracts;
        private readonly IChainStateChangeHandler handler;
        private readonly bool doProofPeriodMonitoring;

        public ChainState(ILog log, IGethNode geth, IPrometheiContracts contracts, IChainStateChangeHandler changeHandler, DateTime startUtc, bool doProofPeriodMonitoring, IPeriodMonitorEventHandler periodEventHandler)
        {
            this.log = new LogPrefixer(log, "(ChainState) ");
            requests = new ChainStateRequestList(this.log);

            this.geth = geth;
            this.contracts = contracts;
            handler = changeHandler;
            this.doProofPeriodMonitoring = doProofPeriodMonitoring;
            TotalSpan = new TimeRange(startUtc, startUtc);
            PeriodMonitor = new PeriodMonitor(log, contracts, geth, periodEventHandler);

            Initialize(startUtc);
        }

        public TimeRange TotalSpan { get; private set; }
        public IChainStateRequest[] Requests => requests.ToArray();
        public PeriodMonitor PeriodMonitor { get; }
        public BlockTimeEntry CurrentBlock { get; private set; } = null!;

        public bool TryRecoverRunningRequest(byte[] requestId)
        {
            var request = FindRequestOnChain(requestId);
            // Request not found by this ID
            if (request == null) return false;

            // Found and relevant:
            if (request.State == RequestState.New ||
                request.State == RequestState.Started)
            {
                requests.Add(request);
                return true;
            }

            // Found, but no longer relevant.
            return false;
        }

        public int Update()
        {
            return Update(DateTime.UtcNow);
        }

        public int Update(DateTime toUtc)
        {
            requests.Cleanup();

            var name = $"{nameof(Update)}({Time.FormatTimestamp(toUtc)})";
            return Stopwatch.Measure(log, name, () => UpdateInternal(toUtc), true).Value;
        }

        private int UpdateInternal(DateTime toUtc)
        {
            var entry = geth.GetHighestBlockBeforeUtc(toUtc);
            if (entry == null) throw new Exception("Unable to find block for update utc: " + Time.FormatTimestamp(toUtc));
            var span = new BlockInterval(new TimeRange(CurrentBlock.Utc, entry.Utc), CurrentBlock.BlockNumber + 1, entry.BlockNumber);
            var events = ChainEvents.FromBlockInterval(contracts, span);
            Apply(events);

            TotalSpan = new TimeRange(TotalSpan.From, entry.Utc);
            CurrentBlock = entry;
            return events.All.Length;
        }

        private void Initialize(DateTime startingUtc)
        {
            var entry = geth.GetHighestBlockBeforeUtc(startingUtc);
            if (entry == null)
            {
                log.Error("Unable to find block for starting utc: " + Time.FormatTimestamp(startingUtc));
                log.Error("Going with block 1 instead...");
                entry = geth.GetBlockForNumber(1);
                if (entry == null) throw new Exception($"Unable to initialize chainstate. Unable to get block at starting UTC {Time.FormatTimestamp(startingUtc)} AND unable to initialize to block number 1.");
            }

            TotalSpan = new TimeRange(TotalSpan.From, entry.Utc);
            CurrentBlock = entry;

            log.Log("Initialized to " + CurrentBlock);
        }

        private void Apply(ChainEvents events)
        {
            if (events.BlockInterval.TimeRange.From < TotalSpan.From)
            {
                var msg = $"Attempt to update ChainState with set of events from before its current record. " +
                    $"TotalSpan: {TotalSpan}" +
                    $"Blocks: {events.BlockInterval} " +
                    $"Times: {events.BlockInterval.TimeRange}";
                handler.OnError(msg);
                throw new Exception(msg);
            }

            log.Debug($"ChainState updating: {events.BlockInterval} = {events.All.Length} events.");

            ApplyInternal(events);
        }

        private void ApplyInternal(ChainEvents events)
        {
            try
            {
                // Run through each block and apply the events to the state in order.
                // Even when there are no events in the list, still run through each block:
                // There might be time-based OR period-based actions that need to happen at each step.
                var blockTimeGetter = new BlockTimeGetter(events.BlockInterval);
                for (var b = events.BlockInterval.From; b <= events.BlockInterval.To; b++)
                {
                    var entry = blockTimeGetter.Get(b);
                    var blockEvents = events.All.Where(e => e.Block.BlockNumber == b).ToArray();
                    ApplyEvents(entry, blockEvents);
                    UpdatePeriodMonitor(entry);
                }
            }
            catch (Exception ex)
            {
                log.Error($"Exception in {nameof(ApplyInternal)}: {ex}");
                throw;
            }
        }

        private void UpdatePeriodMonitor(BlockTimeEntry block)
        {
            if (!doProofPeriodMonitoring) return;
            PeriodMonitor.Update(block, requests.ToArray());
        }

        private void ApplyEvents(BlockTimeEntry entry, IHasBlock[] blockEvents)
        {
            foreach (var e in blockEvents)
            {
                dynamic d = e;
                ApplyEvent(d);
            }

            ApplyTimeImplicitEvents(entry);
        }

        private void ApplyEvent(StorageRequestedEventDTO @event)
        {
            var r = FindRequest(@event);
            if (r == null) return;

            // Important: FindRequest may have created the request object using
            // the current on-chain state. But we're not busy representing the current
            // state. This might be historical! So, if the state of the request is not
            // "new", then we set it to new.
            if (r.State != RequestState.New)
            {
                log.Log($"In applying the create event for request '{r.Id}', it was fetched from the chain in a different " +
                    $"state: '{r.State}'. Setting it to 'new' to represent the current (historical) state.");

                r.UpdateStateFromEvent(@event, RequestState.New);
            }
          
            handler.OnNewRequest(new RequestEvent(@event.Block, r));
        }

        private void ApplyEvent(RequestFulfilledEventDTO @event)
        {
            var r = FindRequest(@event);
            if (r == null) return;
            r.UpdateStateFromEvent(@event, RequestState.Started);
            handler.OnRequestFulfilled(new RequestEvent(@event.Block, r));
        }

        private void ApplyEvent(RequestFailedEventDTO @event)
        {
            var r = FindRequest(@event);
            if (r == null) return;
            r.UpdateStateFromEvent(@event, RequestState.Failed);
            handler.OnRequestFailed(new RequestEvent(@event.Block, r));
        }

        private void ApplyEvent(SlotFilledEventDTO @event)
        {
            var r = FindRequest(@event);
            if (r == null) return;
            var slotIndex = (int)@event.SlotIndex;
            var isRepair = !r.Hosts.IsFilled(slotIndex) && r.Hosts.WasPreviouslyFilled(slotIndex);
            r.Log($"{@event.Block} SlotFilled (host:'{@event.Host}', slotIndex:{@event.SlotIndex}, isRepair:{isRepair})");
            r.Hosts.HostFillsSlot(@event.Host, slotIndex);
            handler.OnSlotFilled(new RequestEvent(@event.Block, r), @event.Host, @event.SlotIndex, isRepair);
        }

        private void ApplyEvent(SlotFreedEventDTO @event)
        {
            var r = FindRequest(@event);
            if (r == null) return;
            var host = r.Hosts.GetHost((int)@event.SlotIndex);
            r.Log($"{@event.Block} SlotFreed (slotIndex:{@event.SlotIndex}, previousHost:{host.AsStr()} )");
            r.Hosts.SlotFreed((int)@event.SlotIndex);
            handler.OnSlotFreed(new RequestEvent(@event.Block, r), @event.SlotIndex);
        }

        private void ApplyEvent(SlotReservationsFullEventDTO @event)
        {
            var r = FindRequest(@event);
            if (r == null) return;
            r.Log($"{@event.Block} SlotReservationsFull (slotIndex:{@event.SlotIndex})");
            handler.OnSlotReservationsFull(new RequestEvent(@event.Block, r), @event.SlotIndex);
        }

        private void ApplyEvent(ProofSubmittedEventDTO @event)
        {
            var id = Base58.Encode(@event.Id);

            var proofOrigin = @event.FindProofOrigin(contracts, requests.ToArray());
            var proofStr = FormatProofOrigin(proofOrigin);

            handler.OnProofSubmitted(@event.Block, id);
        }

        private void ApplyTimeImplicitEvents(BlockTimeEntry entry)
        {
            foreach (var r in requests.ToArray())
            {
                ApplyTimeImplicitCancelledEvent(r, entry);
                ApplyTimeImplicitFinishedEvent(r, entry);
            }
        }

        private void ApplyTimeImplicitFinishedEvent(ChainStateRequest r, BlockTimeEntry entry)
        {
            if (r.State == RequestState.Started
                && r.FinishedUtc < entry.Utc)
            {
                r.UpdateStateFromTime(
                    matchingBlock: entry,
                    eventName: "RequestFinished",
                    newState: RequestState.Finished);
                handler.OnRequestFinished(new RequestEvent(entry, r), HasBeenExtendedByAnotherRequest(r));
            }
        }

        private IChainStateRequest? HasBeenExtendedByAnotherRequest(ChainStateRequest request)
        {
            // If another contract exists for the same CID and
            // it has been started AND it has a later FinishedUTC,
            // then this one is finished but the data is still stored in the network.
            // Downstream apps may want to be aware of this.

            return requests.FirstOrDefault(r =>
                r.Cid == request.Cid &&
                r.State == RequestState.Started &&
                // The renew-contract must last at least 1 hour longer than the old one.
                // Or else it doesn't count.
                r.FinishedUtc > (request.FinishedUtc + TimeSpan.FromHours(1))
            );
        }

        private void ApplyTimeImplicitCancelledEvent(ChainStateRequest r, BlockTimeEntry entry)
        {
            if (r.State == RequestState.New
                && r.ExpiryUtc < entry.Utc)
            {
                r.UpdateStateFromTime(
                    matchingBlock: entry,
                    eventName: "RequestCancelled",
                    newState: RequestState.Cancelled);
                handler.OnRequestCancelled(new RequestEvent(entry, r));
            }
        }

        private ChainStateRequest? FindRequest(IHasRequestId hasRequestId)
        {
            return FindRequest(hasRequestId.RequestId);
        }

        private ChainStateRequest? FindRequest(byte[] requestId)
        {
            var r = requests.SingleOrDefault(r => ByteArrayUtils.Equal(r.RequestId, requestId));
            if (r != null) return r;

            try
            {
                var newRequest = FindRequestOnChain(requestId);
                if (newRequest == null) return null;
                requests.Add(newRequest);
                return newRequest;
            }
            catch (Exception ex)
            {
                var msg = $"Failed to get request with id '{requestId.ToHex()}' from chain: {ex}";
                log.Error(msg);
                handler.OnError(msg);
                return null;
            }
        }

        /// <summary>
        /// Returns null when not found. Throws when call fails.
        /// </summary>
        private ChainStateRequest? FindRequestOnChain(byte[] requestId)
        {
            var request = contracts.GetRequest(requestId);
            if (request == null) return null;
            var state = contracts.GetRequestState(requestId);
            if (state == null) return null;
            return new ChainStateRequest(log, requestId, request, state.Value, GetExtendsExistingContract);
        }

        private ChainStateRequest? GetExtendsExistingContract(ContentId newCid)
        {
            // If we have the same CID is an existing contract that has not finished yet, then
            // this contract is an extend of the previous one.
            return requests.FirstOrDefault(r => 
                r.Cid == newCid &&
                r.State == RequestState.Started &&
                r.FinishedUtc > DateTime.UtcNow
            );
        }

        private string FormatProofOrigin(ProofOrigin? proofOrigin)
        {
            if (proofOrigin != null)
            {
                return $"({proofOrigin.Request.RequestId.ToHex()} slotIndex:{proofOrigin.SlotIndex})";
            }
            return "(Could not identify proof requestId + slot)";
        }

        public class BlockTimeGetter
        {
            private readonly BlockInterval interval;
            private readonly TimeSpan spanPerBlock;

            public BlockTimeGetter(BlockInterval interval)
            {
                var numBlocks = interval.NumberOfBlocks;
                var span = interval.TimeRange.Duration;
                spanPerBlock = span / numBlocks;
                this.interval = interval;
            }

            public BlockTimeEntry Get(ulong blockNumber)
            {
                // It's too time-consuming to get the blockentry for every block in the range.
                // Instead, we make one up!
                // We do this by assuming the blocks in the range are evenly spaced in time.
                // While that's not necessarily true, it's a good enough approximation for our purposes.

                if (blockNumber > interval.To) throw new Exception($"Block number {blockNumber} is out of range {interval} (over)");
                if (blockNumber < interval.From) throw new Exception($"Block number {blockNumber} is out of range {interval} (under)");

                var count = blockNumber - interval.From;
                var blockUtc = interval.TimeRange.From + (count * spanPerBlock);
                var entry = new BlockTimeEntry(blockNumber, blockUtc);
                if (blockUtc > interval.TimeRange.To)
                {
                    throw new InvalidOperationException(
                        $"BlockRange: {interval} " +
                        $"found spanPerBlock: '{Time.FormatDuration(spanPerBlock)}' - " +
                        $"at block {blockNumber} found blockUtc at {Time.FormatTimestamp(blockUtc)} which is past end of time range.");
                }
                return entry;
            }
        }
    }
}
