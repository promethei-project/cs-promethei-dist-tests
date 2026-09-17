using PrometheiClient;
using PrometheiReleaseTests.Utils;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.MarketTests
{
    [TestFixture]
    public class ProofWorkloadTest : MarketplaceAutoBootstrapDistTest
    {
        protected override int NumberOfHosts => 1;
        protected override int NumberOfClients => 1;

        [Test]
        [Combinatorial]
        public void ProofWorkload(
            [Values(12)] int secondsPerProof
        )
        {
            var assumeProofTime = TimeSpan.FromSeconds(secondsPerProof);
            Log($"Assume proof calculation + submit takes: {Time.FormatDuration(assumeProofTime)}");

            var periodDuration = GetContracts().Deployment.Config.PeriodDuration;
            Log($"Proving period: {Time.FormatDuration(periodDuration)}");

            var maxProofsPerPeriod = Convert.ToInt32(Math.Floor(periodDuration / assumeProofTime)) - 1;
            Log($"Max number of proofs one host can reasonably calculate and submit during one period: {maxProofsPerPeriod}");

            var (hosts, clients, validator) = JumpStart();
            var client = clients.Single();

            var request = client.Marketplace.RequestStorage(
                new StoragePurchaseRequest(client.UploadFile(GenerateTestFile(maxProofsPerPeriod.MB())), p => p
                .WithNodes(maxProofsPerPeriod)
                .WithTolerance(1)
                .WithProofProbability(1)
            ));
            
            request.WaitForStorageContractFinished();
        }
    }
}
