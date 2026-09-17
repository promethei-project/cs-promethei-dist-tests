using BlockchainUtils;
using PrometheiContractsPlugin.Marketplace;
using Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Utils;
using PrometheiClient;

namespace PrometheiContractsPlugin.ChainMonitor
{
    public interface IChainStateRequest
    {
        string Id { get; }
        byte[] RequestId { get; }
        RequestState State { get; }
        TimeSpan Expiry { get; }
        DateTime ExpiryUtc { get; }
        DateTime FinishedUtc { get; }
        EthAddress Client { get; }
        ContentId Cid { get; }
        RequestHosts Hosts { get; }
        ChainStateRequestAsk Ask { get; }
        ChainStateRequest? ExtendsExistingContract { get; }
    }

    public class ChainStateRequest : IChainStateRequest
    {
        private readonly ILog log;

        public ChainStateRequest(ILog log, byte[] requestId, CacheRequest cacheRequest, RequestState state, Func<ContentId, ChainStateRequest?> getIsExtend)
        {
            if (requestId == null || requestId.Length != 32) throw new ArgumentException(nameof(requestId));

            this.log = log;
            Id = requestId.ToHex().ToLowerInvariant();
            RequestId = requestId;
            State = state;

            Expiry = TimeSpan.FromSeconds(cacheRequest.Request.Expiry);
            ExpiryUtc = cacheRequest.ExpiryUtc;
            FinishedUtc = cacheRequest.FinishUtc;

            Log($"Created as {State}.");

            var ask = cacheRequest.Request.Ask;
            var slotSize = new ByteSize(Convert.ToInt64(ask.SlotSize));
            var encodedSize = slotSize.Multiply(ask.Slots);

            Client = new EthAddress(cacheRequest.Request.Client);
            Cid = new ContentId("z" + Base58.Encode(cacheRequest.Request.Content.Cid), encodedSize);
            Hosts = new RequestHosts();
            ExtendsExistingContract = getIsExtend(Cid);

            Ask = new ChainStateRequestAsk(
                (int)ask.ProofProbability,
                ask.PricePerBytePerSecond.TstWei(),
                ask.CollateralPerByte.TstWei(),
                ask.Slots,
                slotSize,
                TimeSpan.FromSeconds(ask.Duration),
                ask.MaxSlotLoss
            );
        }

        public string Id { get; }
        public byte[] RequestId { get; }
        public RequestState State { get; private set; }
        public TimeSpan Expiry { get; }
        public DateTime ExpiryUtc { get; }
        public DateTime FinishedUtc { get; }
        public EthAddress Client { get; }
        public ContentId Cid { get; }
        public RequestHosts Hosts { get; }
        public ChainStateRequestAsk Ask { get; }
        public ChainStateRequest? ExtendsExistingContract { get; }

        public void UpdateStateFromEvent(IHasBlockAndRequestId triggeringEvent, RequestState newState)
        {
            Log($"Contract event {triggeringEvent.GetType().Name} at {triggeringEvent.Block} causes Transit: {State} -> {newState}");
            State = newState;
        }

        public void UpdateStateFromTime(BlockTimeEntry matchingBlock, string eventName, RequestState newState)
        {
            Log($"Time event {eventName} at {matchingBlock} causes Transit: {State} -> {newState}");
            State = newState;
        }

        public void Log(string msg)
        {
            log.Log($"Request '{RequestId.ToHex()}': {msg}");
        }
    }

    public class RequestHosts
    {
        private readonly Dictionary<int, EthAddress> hosts = new Dictionary<int, EthAddress>();
        private readonly List<int> filled = new List<int>();

        public void HostFillsSlot(EthAddress host, int index)
        {
            hosts.Add(index, host);
            filled.Add(index);
        }

        public bool IsFilled(int index)
        {
            return hosts.ContainsKey(index);
        }

        public bool WasPreviouslyFilled(int index)
        {
            return filled.Contains(index);
        }
        
        public void SlotFreed(int index)
        {
            hosts.Remove(index);
        }

        public EthAddress? GetHost(int index)
        {
            if (!hosts.ContainsKey(index)) return null;
            return hosts[index];
        }

        public EthAddress[] GetHosts()
        {
            return hosts.Values.ToArray();
        }
    }

    public class ChainStateRequestAsk
    {
        public ChainStateRequestAsk(
            int proofProbability,
            TestToken pricePerBytePerSecond,
            TestToken collateralPerByte,
            ulong slots,
            ByteSize slotSize,
            TimeSpan duration,
            ulong maxSlotLoss
        )
        {
            ProofProbability = proofProbability;
            PricePerBytePerSecond = pricePerBytePerSecond;
            CollateralPerByte = collateralPerByte;
            Slots = slots;
            SlotSize = slotSize;
            Duration = duration;
            MaxSlotLoss = maxSlotLoss;
        }

        public int ProofProbability { get; }
        public TestToken PricePerBytePerSecond { get; }
        public TestToken CollateralPerByte { get; }
        public ulong Slots { get; }
        public ByteSize SlotSize { get; }
        public TimeSpan Duration { get; }
        public ulong MaxSlotLoss { get; }
    }
}
