using PrometheiClient;
using PrometheiPlugin;
using PrometheiReleaseTests.Utils;
using NUnit.Framework;

namespace PrometheiReleaseTests.MarketTests
{
    [TestFixture]
    public class StartTest : MarketplaceAutoBootstrapDistTest
    {
        protected override int NumberOfHosts => 5;
        protected override int NumberOfClients => 1;

        [Test]
        public void Start()
        {
            var (hosts, clients, validator) = JumpStart();
            var client = clients.Single();

            var request = CreateStorageRequest(client);
            request.WaitForStorageContractSubmitted();
            var clientDataset = client.GetDatasetStatus(request.EncodedContentId);
            Assert.That(clientDataset.Blocks.Length, Is.EqualTo(128));

            AssertContractIsOnChain(request);
            WaitForContractStarted(request);
            AssertAllDatasetsAreSameLength(hosts, clientDataset, request);
            AssertContractSlotsAreFilledByHosts(request, hosts, allowExtraSlotBlocks: true);
        }

        private void AssertAllDatasetsAreSameLength(IPrometheiNodeGroup hosts, DatasetStatus clientDataset, IStoragePurchaseContract request)
        {
            Assert.Multiple(() =>
            {
                foreach (var h in hosts)
                {
                    Assert.That(h.GetDatasetStatus(request.EncodedContentId).Blocks.Length, Is.EqualTo(clientDataset.Blocks.Length));
                }
            });
        }

        private IStoragePurchaseContract CreateStorageRequest(IPrometheiNode client)
        {
            var cid = client.UploadFile(GenerateTestFile(PurchaseParams.Default.UploadFilesize));
            return client.Marketplace.RequestStorage(new StoragePurchaseRequest(cid));
        }
    }
}
