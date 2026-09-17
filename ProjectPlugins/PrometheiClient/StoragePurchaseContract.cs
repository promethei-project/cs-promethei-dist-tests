using PrometheiClient.Hooks;
using Logging;
using Newtonsoft.Json;
using Utils;

namespace PrometheiClient
{
    public interface IStoragePurchaseContract
    {
        string PurchaseId { get; }
        StoragePurchaseRequest Purchase { get; }
        ContentId EncodedContentId { get; }
        DateTime GetExpectedFinishUtc();
        StoragePurchase? GetStatus();
        void WaitForStorageContractSubmitted();
        void WaitForStorageContractExpired();
        void WaitForStorageContractStarted();
        void WaitForStorageContractFinished();
        void WaitForContractFailed(IMarketplaceConfigInput config);
    }

    public interface IMarketplaceConfigInput
    {
        int MaxNumberOfSlashes { get; }
        TimeSpan PeriodDuration { get; }
    }

    public class StoragePurchaseContract : IStoragePurchaseContract
    {
        private readonly ILog log;
        private readonly PrometheiAccess prometheiAccess;
        private readonly IPrometheiNodeHooks hooks;
        private readonly TimeSpan gracePeriod = TimeSpan.FromSeconds(60);
        private readonly DateTime contractPendingUtc = DateTime.UtcNow;
        private DateTime? contractSubmittedUtc;
        private DateTime? contractStartedUtc;
        private DateTime? contractFinishedUtc;
        private StoragePurchaseState lastState = StoragePurchaseState.Unknown;
        private ContentId encodedContentId = new ContentId();

        public StoragePurchaseContract(ILog log, PrometheiAccess prometheiAccess, string purchaseId, StoragePurchaseRequest purchase, IPrometheiNodeHooks hooks)
        {
            this.log = log;
            this.prometheiAccess = prometheiAccess;
            PurchaseId = purchaseId;
            Purchase = purchase;
            this.hooks = hooks;
        }

        public string PurchaseId { get; }
        public StoragePurchaseRequest Purchase { get; }
        public ContentId EncodedContentId
        {
            get
            {
                if (string.IsNullOrEmpty(encodedContentId.Id)) GetStatus();
                return encodedContentId;
            }
        }

        public TimeSpan? PendingToSubmitted => contractSubmittedUtc - contractPendingUtc;
        public TimeSpan? SubmittedToStarted => contractStartedUtc - contractSubmittedUtc;
        public TimeSpan? SubmittedToFinished => contractFinishedUtc - contractSubmittedUtc;

        public DateTime GetExpectedFinishUtc()
        {
            if (contractSubmittedUtc == null) throw new Exception("Contract not started yet. Can't predict finish-UTC");
            return contractSubmittedUtc.Value + Purchase.PurchaseParams.Duration;
        }

        public StoragePurchase? GetStatus()
        {
            var status = prometheiAccess.GetPurchaseStatus(PurchaseId);
            if (status != null)
            {
                encodedContentId = new ContentId(status.Request.Content.Cid, Purchase.PurchaseParams.EncodedDatasetSize);
            }
            return status;
        }

        public void WaitForStorageContractSubmitted()
        {
            var timeout = Purchase.PurchaseParams.Expiry + gracePeriod;
            var raiseHook = lastState != StoragePurchaseState.Submitted;
            WaitForStorageContractState(timeout, StoragePurchaseState.Submitted, sleep: 200);
            contractSubmittedUtc = DateTime.UtcNow;
            if (raiseHook) hooks.OnStorageContractSubmitted(this);
            LogSubmittedDuration();
            LogEncodedCid();
            AssertDuration(PendingToSubmitted, timeout, nameof(PendingToSubmitted));
        }

        public void WaitForStorageContractExpired()
        {
            if (!contractSubmittedUtc.HasValue)
            {
                WaitForStorageContractSubmitted();
            }

            var timeout = Purchase.PurchaseParams.Expiry + gracePeriod + gracePeriod;
            WaitForStorageContractState(timeout, StoragePurchaseState.Cancelled);
        }

        public void WaitForStorageContractStarted()
        {
            if (!contractSubmittedUtc.HasValue)
            {
                WaitForStorageContractSubmitted();
            }

            var timeout = Purchase.PurchaseParams.Expiry + gracePeriod;

            WaitForStorageContractState(timeout, StoragePurchaseState.Started);
            contractStartedUtc = DateTime.UtcNow;
            LogStartedDuration();
            LogEncodedCid();
            AssertDuration(SubmittedToStarted, timeout, nameof(SubmittedToStarted));
        }

        public void WaitForStorageContractFinished()
        {
            if (!contractStartedUtc.HasValue)
            {
                WaitForStorageContractStarted();
            }
            var currentContractTime = DateTime.UtcNow - contractSubmittedUtc!.Value;
            var timeout = (Purchase.PurchaseParams.Duration - currentContractTime) + gracePeriod;
            WaitForStorageContractState(timeout, StoragePurchaseState.Finished);
            contractFinishedUtc = DateTime.UtcNow;
            LogFinishedDuration();
            AssertDuration(SubmittedToFinished, timeout, nameof(SubmittedToFinished));
        }

        public void WaitForContractFailed(IMarketplaceConfigInput config)
        {
            if (!contractStartedUtc.HasValue)
            {
                WaitForStorageContractStarted();
            }
            var currentContractTime = DateTime.UtcNow - contractSubmittedUtc!.Value;
            var timeout = (Purchase.PurchaseParams.Duration - currentContractTime) + gracePeriod;
            var minTimeout = TimeNeededToFailEnoughProofsToFreeASlot(config);

            if (timeout < minTimeout)
            {
                throw new ArgumentOutOfRangeException(
                    $"Test is misconfigured. Assuming a proof is required every period, it will take {Time.FormatDuration(minTimeout)} " +
                    $"to fail enough proofs for a slot to be freed. But, the storage contract will complete in {Time.FormatDuration(timeout)}. " +
                    $"Increase the duration."
                );
            }

            WaitForStorageContractState(timeout, StoragePurchaseState.Errored);
        }

        private TimeSpan TimeNeededToFailEnoughProofsToFreeASlot(IMarketplaceConfigInput config)
        {
            var numMissedProofsRequiredForFree = config.MaxNumberOfSlashes;
            var timePerProof = config.PeriodDuration;
            var result = timePerProof * (numMissedProofsRequiredForFree + 1);

            // Times 2!
            // Because of pointer-downtime it's possible that some periods even though there's a probability of 100%
            // will not require any proof. To be safe we take twice the required time.
            return result * 2;
        }

        private void WaitForStorageContractState(TimeSpan timeout, StoragePurchaseState desiredState, int sleep = 1000)
        {
            var waitStart = DateTime.UtcNow;

            Log($"Waiting for {Time.FormatDuration(timeout)} to reach state '{desiredState}'.");
            while (lastState != desiredState)
            {
                Thread.Sleep(sleep);

                var purchaseStatus = prometheiAccess.GetPurchaseStatus(PurchaseId);
                var statusJson = JsonConvert.SerializeObject(purchaseStatus);
                if (purchaseStatus != null && purchaseStatus.State != lastState)
                {
                    lastState = purchaseStatus.State;
                    log.Debug("Purchase status: " + statusJson);
                    hooks.OnStorageContractUpdated(purchaseStatus);
                }

                if (desiredState != StoragePurchaseState.Errored && lastState == StoragePurchaseState.Errored)
                {
                    FrameworkAssert.Fail("Contract errored: " + statusJson);
                }

                if (DateTime.UtcNow - waitStart > timeout)
                {
                    FrameworkAssert.Fail($"Contract did not reach '{desiredState}' within {Time.FormatDuration(timeout)} timeout. {statusJson}");
                }
            }
        }

        private void LogSubmittedDuration()
        {
            Log($"Pending to Submitted in {Time.FormatDuration(PendingToSubmitted)} " +
                $"( < {Time.FormatDuration(Purchase.PurchaseParams.Expiry + gracePeriod)})");
        }

        private void LogEncodedCid()
        {
            // This ensures the encoded CID is fetched. Client node may go offline later.
            Log($"Encoded CID: '{EncodedContentId}'");
        }

        private void LogStartedDuration()
        {
            Log($"Submitted to Started in {Time.FormatDuration(SubmittedToStarted)} " +
                $"( < {Time.FormatDuration(Purchase.PurchaseParams.Expiry + gracePeriod)})");
        }

        private void LogFinishedDuration()
        {
            Log($"Submitted to Finished in {Time.FormatDuration(SubmittedToFinished)} " +
                $"( < {Time.FormatDuration(Purchase.PurchaseParams.Duration + gracePeriod)})");
        }

        private void AssertDuration(TimeSpan? span, TimeSpan max, string message)
        {
            if (span == null) throw new ArgumentNullException(nameof(MarketplaceAccess) + ": " + message + " (IsNull)");
            if (span.Value.TotalDays >= max.TotalSeconds)
            {
                throw new Exception(nameof(MarketplaceAccess) +
                    $": Duration out of range. Max: {Time.FormatDuration(max)} but was: {Time.FormatDuration(span.Value)} " +
                    message);
            }
        }

        private void Log(string msg)
        {
            log.Log($"[{PurchaseId}] {msg}");
        }
    }
}
