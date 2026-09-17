using PrometheiClient;
using PrometheiContractsPlugin.ChainMonitor;
using PrometheiContractsPlugin.Marketplace;
using PrometheiReleaseTests.Utils;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.MarketTests
{
    [TestFixture]
    public class StabilityTest : MarketplaceAutoBootstrapDistTest
    {
        #region Setup

        protected override int NumberOfHosts => 6;
        protected override int NumberOfClients => 1;
        protected override TimeSpan HostAvailabilityMaxDuration => TimeSpan.FromDays(5.0);
        protected override TestToken HostStartingBalance => PurchaseParams.Default.CollateralRequiredPerSlot * 1.1; // Each host can hold 1 slot.

        #endregion

        private int numPeriods = 0;
        private int numProofs = 0;
        private bool proofWasMissed = false;

        [Test]
        [Combinatorial]
        public void Stability(
            [Values(20)] int minutes)
        {
            var mins = TimeSpan.FromMinutes(minutes);
            var periodDuration = GetContracts().Deployment.Config.PeriodDuration;
            Assert.That(HostAvailabilityMaxDuration, Is.GreaterThan(mins * 1.1));

            numPeriods = 0;
            numProofs = 0;
            proofWasMissed = false;

            var (hosts, clients, validator) = JumpStart();
            var client = clients.Single();

            var purchase = CreateStorageRequest(client, mins);
            purchase.WaitForStorageContractStarted();

            Log($"Contract should remain stable for {Time.FormatDuration(mins)}.");
            var endUtc = DateTime.UtcNow + mins;
            while (DateTime.UtcNow < endUtc)
            {
                Thread.Sleep(TimeSpan.FromSeconds(10));
                if (proofWasMissed)
                {
                    // We wait because we want to log calls to MarkProofAsMissing.
                    Thread.Sleep(periodDuration * 1.1);
                    Assert.Fail("Proof was missed.");
                }
            }

            var minNumPeriod = (mins / periodDuration) - 1.0;
            Log($"{numPeriods} periods elapsed. Expected at least {minNumPeriod} periods.");
            Log($"{numProofs} proofs submitted.");
            Assert.That(numPeriods, Is.GreaterThanOrEqualTo(minNumPeriod));

            var status = client.GetPurchaseStatus(purchase.PurchaseId);
            if (status == null) throw new Exception("Purchase status not found");
            Assert.That(status.IsStarted);
        }

        protected override void OnPeriod(PeriodReport report)
        {
            numPeriods++;

            foreach (var r in report.Requests)
            {
                foreach (var s in r.Slots)
                {
                    if (s.GetIsProofMissed())
                    {
                        Log($"A proof was missed. Failing test after a delay so chain events have time to log...");
                        proofWasMissed = true;
                        return;
                    }
                }
            }

            foreach (var func in report.FunctionCalls)
            {
                if (func.Name == nameof(SubmitProofFunction))
                {
                    numProofs++;
                }
            }

            // There can't be any calls to FreeSlot.
            // All slots are correctly filled and proven for the entire duration.
            AssertNoSlotsFreed(report);
        }

        private IStoragePurchaseContract CreateStorageRequest(IPrometheiNode client, TimeSpan minutes)
        {
            var cid = client.UploadFile(GenerateTestFile(PurchaseParams.Default.UploadFilesize));
            var config = GetContracts().Deployment.Config;
            return client.Marketplace.RequestStorage(new StoragePurchaseRequest(cid, p => p
                .WithDuration(minutes * 2.0)
                .WithExpiry(TimeSpan.FromMinutes(8.0))
                .WithProofProbability(1)
            ));
        }
    }
}
