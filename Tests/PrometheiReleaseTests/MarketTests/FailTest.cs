using PrometheiClient;
using PrometheiContractsPlugin.Marketplace;
using PrometheiReleaseTests.Utils;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.MarketTests
{
    public class FailTest : MarketplaceAutoBootstrapDistTest
    {
        protected override int NumberOfHosts => 4;
        protected override int NumberOfClients => 1;
        private readonly int SlotTolerance;

        public FailTest()
        {
            SlotTolerance = NumberOfHosts / 2;
        }

        [Test]
        public void Fail()
        {
            var (hosts, clients, validator) = JumpStart();
            var client = clients.Single();

            var request = CreateStorageRequest(client);

            request.WaitForStorageContractSubmitted();
            AssertContractIsOnChain(request);

            request.WaitForStorageContractStarted();
            AssertContractSlotsAreFilledByHosts(request, hosts, allowExtraSlotBlocks: true);

            hosts.Stop(waitTillStopped: true);

            WaitForSlotFreedEvents();

            var config = GetContracts().Deployment.Config;
            request.WaitForContractFailed(config);
        }

        private void WaitForSlotFreedEvents()
        {
            var start = DateTime.UtcNow;
            var timeout = CalculateContractFailTimespan();

            Log($"{nameof(WaitForSlotFreedEvents)} timeout: {Time.FormatDuration(timeout)}");

            while (DateTime.UtcNow < start + timeout)
            {
                var events = GetContracts().GetEvents(GetTestRunTimeRange());
                var slotFreed = events.GetEvents<SlotFreedEventDTO>();
                Log($"SlotFreed events: {slotFreed.Length} - Expected: {SlotTolerance}");
                if (slotFreed.Length > SlotTolerance)
                {
                    Log($"{nameof(WaitForSlotFreedEvents)} took {Time.FormatDuration(DateTime.UtcNow - start)}");
                    return;
                }
                GetContracts().WaitUntilNextPeriod();
            }
            Assert.Fail($"{nameof(WaitForSlotFreedEvents)} failed after {Time.FormatDuration(timeout)}");
        }

        private IStoragePurchaseContract CreateStorageRequest(IPrometheiNode client)
        {
            var cid = client.UploadFile(GenerateTestFile(3.MB()));
            return client.Marketplace.RequestStorage(new StoragePurchaseRequest(cid, p => p
                .WithDuration(HostAvailabilityMaxDuration / 2)
                .WithNodes(NumberOfHosts)
                .WithTolerance(SlotTolerance)
                .WithProofProbability(1) // Require a proof every period
            ));
        }
    }
}
