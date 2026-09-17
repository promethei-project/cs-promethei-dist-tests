using PrometheiClient;
using PrometheiPlugin;
using PrometheiReleaseTests.Utils;
using NUnit.Framework;

namespace PrometheiReleaseTests.MarketTests
{
    namespace Successful
    {
        [TestFixture(20, 5, 3, 1)]
        [TestFixture(60, 5, 3, 1)]
        [TestFixture(20, 10, 8, 4)]
        [TestFixture(60, 10, 8, 4)]
        public class FinishTest : MarketplaceAutoBootstrapDistTest
        {
            private readonly int hosts;
            private readonly PurchaseParams purchaseParams;

            public FinishTest(int durationMinutes, int hosts, int slots, int tolerance)
            {
                this.hosts = hosts;
                purchaseParams = PurchaseParams.Default
                    .WithDuration(TimeSpan.FromMinutes(durationMinutes))
                    .WithNodes(slots)
                    .WithTolerance(tolerance);
            }

            protected override int NumberOfHosts => hosts;
            protected override int NumberOfClients => 1;

            [Test]
            public void Finish()
            {
                var (hosts, clients, validator) = JumpStart();
                var client = clients.Single();

                AssertHostsAreEmpty(hosts);

                var request = CreateStorageRequest(client);

                request.WaitForStorageContractSubmitted();
                AssertContractIsOnChain(request);

                WaitForContractStarted(request);
                AssertContractSlotsAreFilledByHosts(request, hosts, allowExtraSlotBlocks: true);

                foreach (var h in hosts) h.GetDatasetStatus(request.EncodedContentId);

                AssertHostsHoldData(hosts, request);

                Log("We wait for most of the contract time and then check again.");
                Sleep((request.GetExpectedFinishUtc() - DateTime.UtcNow) - TimeSpan.FromSeconds(30));
                Log("We expect the contract to be running and the hosts to hold the data.");
                Assert.That(request.GetStatus()?.IsStarted, Is.EqualTo(true));
                AssertHostsHoldData(hosts, request);

                request.WaitForStorageContractFinished();

                Log("Now the request is finished.");
                Log("We expect the hosts and client balances to reflect the payment for the provided storage.");
                AssertClientHasPaidForContract(PurchaseParams.Default.PricePerByteSecond, client, request, hosts);
                AssertHostsWerePaidForContract(PurchaseParams.Default.PricePerByteSecond, request, hosts);
                AssertHostsCollateralsAreUnchanged(hosts);
                Log("We expect the hosts to be empty.");
                AssertHostsAreEmpty(hosts);
            }

            private void AssertHostsHoldData(IPrometheiNodeGroup hosts, IStoragePurchaseContract request)
            {
                var fills = GetOnChainSlotFills(hosts);
                foreach (var f in fills)
                {
                    AssertHostHoldsSlot(f, request, allowExtras: true);
                }
            }

            private IStoragePurchaseContract CreateStorageRequest(IPrometheiNode client)
            {
                var cid = client.UploadFile(GenerateTestFile(purchaseParams.UploadFilesize));
                return client.Marketplace.RequestStorage(new StoragePurchaseRequest(cid, purchaseParams));
            }
        }
    }
}
