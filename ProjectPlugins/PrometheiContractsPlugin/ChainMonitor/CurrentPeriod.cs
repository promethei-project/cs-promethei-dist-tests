using BlockchainUtils;
using Utils;

namespace PrometheiContractsPlugin.ChainMonitor
{
    public class CurrentPeriod
    {
        public CurrentPeriod(BlockTimeEntry startingBlock, ulong periodNumber, CurrentRequest[] currentRequests)
        {
            StartingBlock = startingBlock;
            PeriodNumber = periodNumber;
            CurrentRequests = currentRequests;
        }

        public BlockTimeEntry StartingBlock { get; }
        public ulong PeriodNumber { get; }
        public CurrentRequest[] CurrentRequests { get; }
    }

    public class CurrentRequest
    {
        public static CurrentRequest CreateCurrentRequest(IPrometheiContracts contracts, IChainStateRequest r)
        {
            var cSlots = new List<CurrentRequestSlot>();
            for (ulong slotIndex = 0; slotIndex < r.Ask.Slots; slotIndex++)
            {
                cSlots.Add(CurrentRequestSlot.CreateCurrentRequestSlot(contracts, r, slotIndex));
            };

            return new CurrentRequest(r, cSlots.ToArray());
        }

        public CurrentRequest(IChainStateRequest request, CurrentRequestSlot[] slots)
        {
            Request = request;
            Slots = slots;
        }

        public IChainStateRequest Request { get; }
        public CurrentRequestSlot[] Slots { get; }
    }

    public class CurrentRequestSlot
    {
        public static CurrentRequestSlot CreateCurrentRequestSlot(IPrometheiContracts contracts, IChainStateRequest r, ulong slotIndex)
        {
            var isRequired = contracts.IsProofRequired(r.RequestId, slotIndex);
            var willRequire = contracts.WillProofBeRequired(r.RequestId, slotIndex);
            var idx = Convert.ToInt32(slotIndex);
            var host = r.Hosts.GetHost(idx);
            var slotId = contracts.GetSlotId(r.RequestId, slotIndex);
            return new CurrentRequestSlot(idx, slotId, host, isRequired, willRequire);
        }

        public CurrentRequestSlot(int index, byte[] slotId, EthAddress? host,
            bool isProofRequired,
            bool willProofBeRequired)
        {
            Index = index;
            SlotId = slotId;
            Host = host;
            IsProofRequired = isProofRequired;
            WillProofBeRequired = willProofBeRequired;
        }

        public int Index { get; }
        public byte[] SlotId { get; }
        public EthAddress? Host { get; }
        public bool IsProofRequired { get; }
        public bool WillProofBeRequired { get; }
    }
}
