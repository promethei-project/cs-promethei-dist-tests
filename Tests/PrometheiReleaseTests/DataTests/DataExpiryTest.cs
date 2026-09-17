using PrometheiClient;
using PrometheiContractsPlugin;
using PrometheiPlugin;
using PrometheiTests;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.DataTests
{
    [TestFixture]
    public class DataExpiryTest : PrometheiDistTest
    {
        private readonly TimeSpan blockTtl = TimeSpan.FromMinutes(1.0);
        private readonly TimeSpan blockInterval = TimeSpan.FromSeconds(10.0);
        private readonly int blockCount = 100000;

        private IPrometheiSetup WithFastBlockExpiry(IPrometheiSetup setup)
        {
            return setup
                .WithBlockTTL(blockTtl)
                .WithBlockMaintenanceInterval(blockInterval)
                .WithBlockMaintenanceNumber(blockCount);
        }

        [Test]
        [Combinatorial]
        public void DeletesExpiredData(
            [Values(1, 500)] int uploads)
        {
            var fileSize = 3.MB();
            var node = StartPromethei(s => WithFastBlockExpiry(s));

            var startSpace = node.Space();
            Assert.That(startSpace.QuotaUsedBytes, Is.EqualTo(0));

            for (var i = 0; i < uploads; i++)
            {
                node.UploadFile(GenerateTestFile(fileSize));
            }

            // This assumes that 3MB files can be uploaded 'uploads' times
            // within the 'blockTtl' timeout. This should be true, the upload
            // should be fast enough!
            var usedSpace = node.Space();
            var usedFiles = node.LocalFiles();
            Assert.That(usedSpace.QuotaUsedBytes, Is.GreaterThanOrEqualTo(fileSize.SizeInBytes));
            Assert.That(usedSpace.FreeBytes, Is.LessThanOrEqualTo(startSpace.FreeBytes - (uploads * fileSize.SizeInBytes)));
            Assert.That(usedFiles.Content.Length, Is.EqualTo(uploads));

            WaitAndAssertEmpty(node, usedSpace, startSpace);
        }

        [Test]
        public void DeletesExpiredDataUsedByStorageRequests()
        {
            var fileSize = 3.MB();

            var bootstrapNode = StartPromethei();
            var geth = StartGethNode(s => s.IsMiner());
            var contracts = Ci.StartPrometheiContracts(s => s
                .WithRpcNode(geth)
                .WithVersionInfo(bootstrapNode.Version)
            );
            var node = StartPromethei(s => WithFastBlockExpiry(s)
                .EnableMarketplace(geth, contracts, m => m.WithInitial(100.Eth(), 100.Tst()))
            );

            var startSpace = node.Space();
            Assert.That(startSpace.QuotaUsedBytes, Is.EqualTo(0));

            var cid = node.UploadFile(GenerateTestFile(fileSize));
            var purchase = node.Marketplace.RequestStorage(new StoragePurchaseRequest(cid));
            var usedSpace = node.Space();
            var usedFiles = node.LocalFiles();
            Assert.That(usedSpace.QuotaUsedBytes, Is.GreaterThanOrEqualTo(fileSize.SizeInBytes));
            Assert.That(usedSpace.FreeBytes, Is.LessThanOrEqualTo(startSpace.FreeBytes - fileSize.SizeInBytes));
            Assert.That(usedFiles.Content.Length, Is.EqualTo(2));

            WaitAndAssertEmpty(node, usedSpace, startSpace);
        }

        [Test]
        public void StorageRequestsKeepManifests()
        {
            var bootstrapNode = StartPromethei(s => s.WithName("Bootstrap"));
            var geth = StartGethNode(s => s.IsMiner());
            var contracts = Ci.StartPrometheiContracts(s => s
                .WithRpcNode(geth)
                .WithVersionInfo(bootstrapNode.Version)
            );
            var client = StartPromethei(s => WithFastBlockExpiry(s)
                .WithName("client")
                .WithBootstrapNode(bootstrapNode)
                .EnableMarketplace(geth, contracts, m => m.WithInitial(100.Eth(), 100.Tst()))
            );

            var hosts = StartPromethei(3, s => WithFastBlockExpiry(s)
                .WithName("host")
                .WithBootstrapNode(bootstrapNode)
                .EnableMarketplace(geth, contracts, m => m.AsStorageNode().WithInitial(100.Eth(), 100.Tst()))
            );
            foreach (var host in hosts) host.Marketplace.MakeStorageAvailable(new CreateStorageAvailability(
                maxDuration: TimeSpan.FromDays(2.0),
                untilUtc: DateTime.UtcNow + TimeSpan.FromDays(30.0),
                minPricePerBytePerSecond: 1.TstWei(),
                maxCollateralPerByte: 10.Tst()));

            var uploadCid = client.UploadFile(GenerateTestFile(5.MB()));
            var request = client.Marketplace.RequestStorage(new StoragePurchaseRequest(uploadCid, p => p
                .WithDuration(TimeSpan.FromDays(1.0))
                .WithExpiry(TimeSpan.FromHours(1.0))
                .WithNodes(3)
                .WithTolerance(1)
                .WithPricePerByteSecond(10.TstWei())
                .WithProofProbability(99999)
            ));
            request.WaitForStorageContractSubmitted();
            request.WaitForStorageContractStarted();
            var storeCid = request.EncodedContentId;

            var clientManifest = client.DownloadManifestOnly(storeCid);
            Assert.That(clientManifest.Manifest.Protected, Is.True);

            client.Stop(waitTillStopped: true);
            Sleep(blockTtl * 2.0);

            var checker = StartPromethei(s => s.WithName("checker").WithBootstrapNode(bootstrapNode));
            var manifest = checker.DownloadManifestOnly(storeCid);
            Assert.That(manifest.Manifest.Protected, Is.True);
        }

        private void WaitAndAssertEmpty(IPrometheiNode node, PrometheiSpace usedSpace, PrometheiSpace startSpace)
        {
            Sleep(blockTtl * 2);

            var cleanupSpace = node.Space();
            var cleanupFiles = node.LocalFiles();

            Assert.That(cleanupSpace.QuotaUsedBytes, Is.LessThan(usedSpace.QuotaUsedBytes));
            Assert.That(cleanupSpace.FreeBytes, Is.GreaterThan(usedSpace.FreeBytes));
            Assert.That(cleanupFiles.Content.Length, Is.EqualTo(0));

            Assert.That(cleanupSpace.QuotaUsedBytes, Is.EqualTo(startSpace.QuotaUsedBytes));
            Assert.That(cleanupSpace.FreeBytes, Is.EqualTo(startSpace.FreeBytes));
        }
    }
}
