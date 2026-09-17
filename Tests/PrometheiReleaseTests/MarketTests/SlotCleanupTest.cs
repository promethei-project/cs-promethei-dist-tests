using PrometheiClient;
using PrometheiReleaseTests.Utils;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.MarketTests
{
    [TestFixture]
    public class SlotCleanupTest : MarketplaceAutoBootstrapDistTest
    {
        protected override int NumberOfHosts => 6;
        protected override int NumberOfClients => 1;

        protected override TestToken HostStartingBalance => PurchaseParams.Default.CollateralRequiredPerSlot * 1.1;
        protected override TimeSpan HostBlockTTL => TimeSpan.FromMinutes(1.0);

        [Test]
        public void SlotCleanup()
        {
            var (hosts, clients) = JumpStartHostsAndClients();
            var client = clients.Single();

            var uploadCid = client.UploadFile(GenerateTestFile(PurchaseParams.Default.UploadFilesize));
            var contract = client.Marketplace.RequestStorage(new StoragePurchaseRequest(uploadCid));
            contract.WaitForStorageContractStarted();
            var contractManifest = client.DownloadManifestOnly(contract.EncodedContentId);
            client.Stop(waitTillStopped: false);

            var fills = GetOnChainSlotFills(hosts);
            var slotHosts = GetHostsThatFilledASlot(hosts, fills);
            var emptyHosts = GetHostsThatFilledNoSlots(hosts, fills);

            Log("Check the expected number of slot hosts and empty hosts...");
            Assert.That(slotHosts.Length, Is.EqualTo(4));
            Assert.That(emptyHosts.Length, Is.EqualTo(2));

            Log("We wait for the contract expiry timeout so the hosts will want to clean up the blocks of failed slots...");
            Sleep(PurchaseParams.Default.Expiry);
            Log("We wait for block maintenance interval (x2) ...");
            Sleep(HostBlockTTL * 2.0);

            Log("Now we check the empty hosts are actually empty...");
            AssertHostsAreEmpty(emptyHosts);

            Log("And we check that the slot hosts are holding exactly 1 slot each...");

            var slotBlocks = PurchaseParams.Default.SlotSize.DivUp(contractManifest.Manifest.BlockSize);
            Log($"Slot size: {PurchaseParams.Default.SlotSize} - Block size: {contractManifest.Manifest.BlockSize}");
            Log($"We expect {slotBlocks} slot blocks + 1 manifest block");

            ShowBlocks(contract.EncodedContentId, slotHosts);
            Assert.Multiple(() =>
            {
                foreach (var h in slotHosts)
                {
                    var hostSlots = h.Marketplace.GetSlots();
                    var space = h.Space();
                    Assert.That(hostSlots.Length, Is.EqualTo(1));
                    Assert.That(space.TotalBlocks, Is.EqualTo(slotBlocks + 1));
                }
            });

            Assert.Multiple(() =>
            {
                foreach (var f in fills)
                {
                    AssertHostHoldsSlot(f, contract, allowExtras: false);
                }
            });

            Log("Now we wait till the contract is finished. Then all hosts should return to empty.");
            contract.WaitForStorageContractFinished();
            Sleep(TimeSpan.FromMinutes(1.0));

            AssertHostsAreEmpty(hosts);
        }

        private IPrometheiNode[] GetHostsThatFilledASlot(PrometheiPlugin.IPrometheiNodeGroup hosts, SlotFill[] fills)
        {
            return
                hosts.Where(h =>
                fills.Any(f => f.Host.GetName() == h.GetName()))
                .ToArray();
        }

        private IPrometheiNode[] GetHostsThatFilledNoSlots(PrometheiPlugin.IPrometheiNodeGroup hosts, SlotFill[] fills)
        {
            return
                hosts.Where(h =>
                fills.All(f => f.Host.GetName() != h.GetName()))
                .ToArray();
        }
    }
}
