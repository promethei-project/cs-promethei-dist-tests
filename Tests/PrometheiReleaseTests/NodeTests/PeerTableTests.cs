using PrometheiClient;
using PrometheiTests;
using NUnit.Framework;

namespace PrometheiReleaseTests.NodeTests
{
    [TestFixture]
    public class PeerTableTests : AutoBootstrapDistTest
    {
        [Test]
        public void PeerTableCompleteness()
        {
            var nodes = StartPromethei(10);

            AssertAllNodesSeeEachOther(nodes.Concat([BootstrapNode!]));
        }

        private void AssertAllNodesSeeEachOther(IEnumerable<IPrometheiNode> nodes)
        {
            var helper = CreatePeerConnectionTestHelpers();
            helper.AssertFullyConnected(nodes);
        }
    }
}
