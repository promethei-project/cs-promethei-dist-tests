using PrometheiClient;
using PrometheiReleaseTests.Utils;
using NUnit.Framework;

namespace PrometheiReleaseTests.MarketTests
{
    [TestFixture]
    public class RenewContractTest : MarketplaceAutoBootstrapDistTest
    {
        protected override int NumberOfHosts => 3;
        protected override int NumberOfClients => 1;

        [Test]
        public void RenewMyOwnStorageContract()
        {
            var (hosts, clients) = JumpStartHostsAndClients();
            var client = clients.Single();

            var firstContract = StartFirstContract(client);

            // Using the CID of the contract, we can create another one.
            var secondContract = client.Marketplace.RequestStorage(new StoragePurchaseRequest(firstContract.EncodedContentId));

            Assert.That(firstContract.PurchaseId, Is.Not.EqualTo(secondContract.PurchaseId));

            secondContract.WaitForStorageContractStarted();

            AssertTwoSimilarRequestsOnChain();
        }

        [Test]
        public void RenewSomeoneElsesStorageContract()
        {
            var (hosts, clients) = JumpStartHostsAndClients();
            var client = clients.Single();
            var otherClient = StartClients().Single();

            var firstContract = StartFirstContract(client);

            // We use a different client node to renew the first contract.
            var secondContract = otherClient.Marketplace.RequestStorage(new StoragePurchaseRequest(firstContract.EncodedContentId));

            Assert.That(firstContract.PurchaseId, Is.Not.EqualTo(secondContract.PurchaseId));

            secondContract.WaitForStorageContractStarted();

            AssertTwoSimilarRequestsOnChain();
        }

        private IStoragePurchaseContract StartFirstContract(IPrometheiNode client)
        {
            var cid = client.UploadFile(GenerateTestFile(PurchaseParams.Default.UploadFilesize));
            var firstContract = client.Marketplace.RequestStorage(new StoragePurchaseRequest(cid, p => p.WithDuration(HostAvailabilityMaxDuration * 0.99)));

            firstContract.WaitForStorageContractStarted();

            return firstContract;
        }

        private void AssertTwoSimilarRequestsOnChain()
        {
            var onChainRequests = GetChainMonitor().Requests.ToArray();

            Assert.That(onChainRequests.Length, Is.EqualTo(2));
            var a = onChainRequests[0];
            var b = onChainRequests[1];

            // These values should be identical
            Assert.That(a.Cid.Id, Is.EqualTo(b.Cid.Id));
            Assert.That(a.Ask.Slots, Is.EqualTo(b.Ask.Slots));
            Assert.That(a.Ask.MaxSlotLoss, Is.EqualTo(b.Ask.MaxSlotLoss));
            Assert.That(a.Ask.ProofProbability, Is.EqualTo(b.Ask.ProofProbability));
            // If the slotsize of the new contract is larger than the original one,
            // probably we have double-encoding. We don't want that.
            Assert.That(a.Ask.SlotSize.SizeInBytes, Is.EqualTo(b.Ask.SlotSize.SizeInBytes));

            // These values must be different
            Assert.That(a.Id, Is.Not.EqualTo(b.Id));
            Assert.That(a.FinishedUtc, Is.Not.EqualTo(b.FinishedUtc).Within(TimeSpan.FromSeconds(3)));
            Assert.That(a.ExpiryUtc, Is.Not.EqualTo(b.ExpiryUtc).Within(TimeSpan.FromSeconds(3)));
        }
    }
}
