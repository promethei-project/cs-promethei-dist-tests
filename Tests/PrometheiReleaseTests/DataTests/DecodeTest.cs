using PrometheiClient;
using PrometheiReleaseTests.Utils;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.DataTests
{
    public class DecodeTest : MarketplaceAutoBootstrapDistTest
    {
        protected override int NumberOfHosts => 0;
        protected override int NumberOfClients => 2;
        protected override TimeSpan HostAvailabilityMaxDuration => TimeSpan.FromSeconds(0.0);

        [Test]
        public void DecodeDataset()
        {
            var clients = StartClients();

            var file = GenerateTestFile(10.MB());
            var bCid = clients[0].UploadFile(file);
            var request = clients[0].Marketplace.RequestStorage(new StoragePurchaseRequest(bCid));
            var eCid = request.EncodedContentId;

            Assert.That(bCid.Id, Is.Not.EqualTo(eCid.Id));

            var basic = clients[0].DownloadManifestOnly(bCid);
            var encoded = clients[0].DownloadManifestOnly(eCid);
            Assert.That(basic.Manifest.Protected, Is.False);
            Assert.That(encoded.Manifest.Protected, Is.True);

            var decoded = clients[1].DownloadContent(eCid);

            file.AssertIsEqual(decoded);
        }

        [Test]
        public void PartiallyDeletedDatasets()
        {
            var clients = StartClients(s => s
                .WithBlockMaintenanceNumber(1)
                .WithBlockMaintenanceInterval(TimeSpan.FromSeconds(10.0))
                .WithBlockTTL(TimeSpan.FromSeconds(30.0)));

            var file = GenerateTestFile(6.MB());
            var bCid = clients[0].UploadFile(file);

            var space = clients[0].Space();
            var update = space;
            while (space.QuotaUsedBytes == update.QuotaUsedBytes)
            {
                Thread.Sleep(TimeSpan.FromSeconds(10.0));
                update = clients[0].Space();
            }

            Assert.That(update.QuotaUsedBytes, Is.LessThan(space.QuotaUsedBytes));
            Log("The dataset is partially deleted.");

            Log("We expect a call to create a storage request for this dataset will fail.");
            try
            {
                var request = clients[0].Marketplace.RequestStorage(new StoragePurchaseRequest(bCid));
                Assert.Fail("Created storage request for partial dataset. Should have failed.");
            }
            catch (TimeoutException)
            {
                Log("Call failed successfully!");
            }

            WaitAndCheckNodesStaysAlive(TimeSpan.FromMinutes(1), clients);
        }
    }
}
