using ArchivistClient;
using ArchivistReleaseTests.Utils;
using NUnit.Framework;

namespace ArchivistReleaseTests.MarketTests
{
    [TestFixture]
    public class SingleNode : MarketplaceAutoBootstrapDistTest
    {
        protected override int NumberOfHosts => 1;
        protected override int NumberOfClients => 0;

        [Test]
        public void ClientIsHost()
        {
            var host = StartHosts().Single();

            var cid = host.UploadFile(GenerateTestFile(PurchaseParams.Default.UploadFilesize));
            var request = host.Marketplace.RequestStorage(new StoragePurchaseRequest(cid));

            request.WaitForStorageContractStarted();

            request.WaitForStorageContractFinished();
        }
    }
}
