using PrometheiClient.Hooks;
using Logging;
using Utils;

namespace PrometheiClient
{
    public interface IMarketplaceAccess
    {
        void MakeStorageAvailable(CreateStorageAvailability availability);
        StorageAvailability GetAvailability();
        string[] GetSlots();
        StorageSlotItem GetSlot(string slotId);
        IStoragePurchaseContract RequestStorage(StoragePurchaseRequest purchase);
    }

    public class MarketplaceAccess : IMarketplaceAccess
    {
        private readonly ILog log;
        private readonly PrometheiAccess prometheiAccess;
        private readonly IPrometheiNodeHooks hooks;

        public MarketplaceAccess(ILog log, PrometheiAccess prometheiAccess, IPrometheiNodeHooks hooks)
        {
            this.log = log;
            this.prometheiAccess = prometheiAccess;
            this.hooks = hooks;
        }

        public IStoragePurchaseContract RequestStorage(StoragePurchaseRequest purchase)
        {
            purchase.Log(log);
            if (purchase.Expiry < TimeSpan.FromMinutes(6.0)) throw new Exception($"Expiry should be at least 6 minutes. Was: {Time.FormatDuration(purchase.Expiry)}");
            if (purchase.Duration < purchase.Expiry) throw new Exception($"Duration must be larger than expiry. Duration: {Time.FormatDuration(purchase.Duration)} Expiry: {Time.FormatDuration(purchase.Expiry)}");

            var swResult = Stopwatch.Measure(log, nameof(RequestStorage), () =>
            {
                return prometheiAccess.RequestStorage(purchase);
            });

            var response = swResult.Value;

            if (string.IsNullOrEmpty(response) ||
                response == "Unable to encode manifest" ||
                response == "Purchasing not available" ||
                response == "Expiry required" ||
                response == "Expiry needs to be in future" ||
                response == "Expiry has to be before the request's end (now + duration)")
            {
                throw new InvalidOperationException(response);
            }

            Log($"Storage requested successfully. PurchaseId: '{response}'.");

            var logName = $"<Purchase-{response.Substring(0, 3)}>";
            log.AddStringReplace(response, logName);
            return new StoragePurchaseContract(log, prometheiAccess, response, purchase, hooks);
        }

        public void MakeStorageAvailable(CreateStorageAvailability availability)
        {
            availability.Log(log);

            prometheiAccess.SalesAvailability(availability);

            Log($"Storage successfully made available.");
            hooks.OnStorageAvailabilityCreated();
        }

        public StorageAvailability GetAvailability()
        {
            var result = prometheiAccess.GetAvailability();
            Log($"Got availability:");
            result.Log(log);
            return result;
        }

        public string[] GetSlots()
        {
            var result = prometheiAccess.GetSlots();
            Log("Active slots: " + result);
            return result;
        }

        public StorageSlotItem GetSlot(string slotId)
        {
            var result = prometheiAccess.GetSlot(slotId);
            result.Log(log);
            return result;
        }

        private void Log(string msg)
        {
            log.Log(msg);
        }
    }

    public class MarketplaceUnavailable : IMarketplaceAccess
    {
        public void MakeStorageAvailable(CreateStorageAvailability availability)
        {
            Unavailable();
            throw new NotImplementedException();
        }

        public IStoragePurchaseContract RequestStorage(StoragePurchaseRequest purchase)
        {
            Unavailable();
            throw new NotImplementedException();
        }

        public StorageAvailability GetAvailability()
        {
            Unavailable();
            throw new NotImplementedException();
        }

        public string[] GetSlots()
        {
            Unavailable();
            throw new NotImplementedException();
        }

        public StorageSlotItem GetSlot(string slotId)
        {
            Unavailable();
            throw new NotImplementedException();
        }

        private void Unavailable()
        {
            FrameworkAssert.Fail("Incorrect test setup: Marketplace was not enabled for this group of Promethei nodes. Add 'EnableMarketplace(...)' after 'SetupPrometheiNodes()' to enable it.");
            throw new InvalidOperationException();
        }
    }
}
