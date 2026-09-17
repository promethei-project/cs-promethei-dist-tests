using Logging;
using Utils;

namespace PrometheiClient
{
    public class StoragePurchaseRequest
    {
        public StoragePurchaseRequest(ContentId cid)
            : this(cid, PurchaseParams.Default)
        {
        }

        public StoragePurchaseRequest(ContentId cid, Func<PurchaseParams, PurchaseParams> decorator)
            : this(cid, decorator(PurchaseParams.Default))
        {
        }

        public StoragePurchaseRequest(ContentId cid, PurchaseParams purchaseParams)
        {
            Cid = cid;
            PurchaseParams = purchaseParams.WithUploadFilesize(cid.KnownFilesize);
        }

        public ContentId Cid { get; }
        public PurchaseParams PurchaseParams { get; }

        public void Log(ILog log)
        {
            log.Log($"Requesting storage for: {Cid.Id} {PurchaseParams}");
        }
    }

    public class StoragePurchase
    {
        public StoragePurchaseState State { get; set; } = StoragePurchaseState.Unknown;
        public string Error { get; set; } = string.Empty;
        public StorageRequest Request { get; set; } = null!;

        public bool IsCancelled => State == StoragePurchaseState.Cancelled;
        public bool IsError => State == StoragePurchaseState.Errored;
        public bool IsFinished => State == StoragePurchaseState.Finished;
        public bool IsStarted => State == StoragePurchaseState.Started;
        public bool IsSubmitted => State == StoragePurchaseState.Submitted;
    }

    public enum StoragePurchaseState
    {
        Cancelled = 0,
        Errored = 1,
        Failed = 2,
        Finished = 3,
        Pending = 4,
        Started = 5,
        Submitted = 6,
        Unknown = 7,
    }

    public class StorageRequest
    {
        public string Id { get; set; } = string.Empty;
        public string Client { get; set; } = string.Empty;
        public StorageAsk Ask { get; set; } = null!;
        public StorageContent Content { get; set; } = null!;
        public long Expiry { get; set; }
        public string Nonce { get; set; } = string.Empty;
    }

    public class StorageAsk
    {
        public long Slots { get; set; }
        public long SlotSize { get; set; }
        public long Duration { get; set; }
        public string ProofProbability { get; set; } = string.Empty;
        public string PricePerBytePerSecond { get; set; } = string.Empty;
        public long MaxSlotLoss { get; set; }
    }

    public class StorageContent
    {
        public string Cid { get; set; } = string.Empty;
    }

    public class CreateStorageAvailability
    {
        public CreateStorageAvailability(TimeSpan maxDuration, DateTime untilUtc, TestToken minPricePerBytePerSecond, TestToken maxCollateralPerByte)
        {
            MaxDuration = maxDuration;
            UntilUtc = untilUtc;
            MinPricePerBytePerSecond = minPricePerBytePerSecond;
            MaxCollateralPerByte = maxCollateralPerByte;
        }

        public TimeSpan MaxDuration { get; }
        public DateTime UntilUtc { get; }
        public TestToken MinPricePerBytePerSecond { get; }
        public TestToken MaxCollateralPerByte { get; }

        public void Log(ILog log)
        {
            log.Log($"Create storage Availability: (" +
                $"maxDuration: {Time.FormatDuration(MaxDuration)}, " +
                $"untilUtc: {Time.FormatTimestamp(UntilUtc)}, " +
                $"minPricePerBytePerSecond: {MinPricePerBytePerSecond}, " +
                $"maxCollateralPerByte: {MaxCollateralPerByte})");
        }
    }

    public class StorageAvailability
    {
        public StorageAvailability(TimeSpan maxDuration, DateTime untilUtc, TestToken minPricePerBytePerSecond, TestToken maxCollateralPerByte)
        {
            MaxDuration = maxDuration;
            UntilUtc = untilUtc;
            MinPricePerBytePerSecond = minPricePerBytePerSecond;
            MaxCollateralPerByte = maxCollateralPerByte;
        }

        public TimeSpan MaxDuration { get; }
        public DateTime UntilUtc { get; }
        public TestToken MinPricePerBytePerSecond { get; }
        public TestToken MaxCollateralPerByte { get; } 

        public void Log(ILog log)
        {
            log.Log($"Storage Availability: (" +
                $"maxDuration: {Time.FormatDuration(MaxDuration)}, " + 
                $"minPricePerBytePerSecond: {MinPricePerBytePerSecond}, " +
                $"maxCollateralPerByte: {MaxCollateralPerByte}, " +
                $"untilUtc: {Time.FormatTimestamp(UntilUtc)})");
        }
    }

    public enum StorageSlotState
    {
        Cancelled = 0,
        Downloading = 1,
        Errored = 2,
        Failed = 3,
        Filled = 4,
        Filling = 5,
        Finished = 6,
        Ignored = 7,
        InitialProving = 8,
        Payout = 9,
        Preparing = 10,
        Proving = 11,
        Unknown = 12,
    }

    public class StorageSlotItem
    {
        public string SlotId { get; set; } = string.Empty;
        public long SlotIndex { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public StorageRequest Request { get; set; } = new StorageRequest();
        public StorageSlotState State { get; set; }

        public void Log(ILog log)
        {
            log.Log($"Storage Slot Item: (" +
                $"requestId: {RequestId}, " +
                $"slotIndex: {SlotIndex}, " +
                $"state: {State})");
        }
    }
}
