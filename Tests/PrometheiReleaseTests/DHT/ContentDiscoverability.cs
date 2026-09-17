using PrometheiTests;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.DHT
{
    [TestFixture]
    public class ContentDiscoverability : AutoBootstrapDistTest
    {
        [Test]
        [Combinatorial]
        public void FullDiscoverabilityWithFailureRate(
            [Values(2, 3)] int failureRate)
        {
            var nodes = StartPromethei(6);

            Log($"Set failure probability: {failureRate}");
            foreach (var n in nodes) n.SetDHTFailureProbability(failureRate);

            Log("Gives network situation time to affect the DHT records.");
            Sleep(TimeSpan.FromMinutes(6));

            var helper = CreatePeerDownloadTestHelpers(
                downloadTimeout: TimeSpan.FromSeconds(10),
                numTries: 3
            );

            helper.AssertFullDownloadInterconnectivity(nodes, 1.MB());
        }
    }
}
