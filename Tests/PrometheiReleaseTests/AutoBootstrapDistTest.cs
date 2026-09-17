using PrometheiClient;
using PrometheiPlugin;
using NUnit.Framework;

namespace PrometheiTests
{
    public class AutoBootstrapDistTest : PrometheiDistTest
    {
        private bool isBooting = false;

        public IPrometheiNode BootstrapNode { get; private set; } = null!;

        [SetUp]
        public void SetupBootstrapNode()
        {
            isBooting = true;
            BootstrapNode = StartPromethei(s =>
            {
                s.WithName("BOOTSTRAP");
                OnBootstrapNodeSetup(s);
            });
            isBooting = false;
        }

        [TearDown]
        public void TearDownBootstrapNode()
        {
            BootstrapNode.Stop(waitTillStopped: true);
        }

        protected virtual void OnBootstrapNodeSetup(IPrometheiSetup setup)
        {
        }

        protected override void OnPrometheiSetup(IPrometheiSetup setup)
        {
            if (isBooting) return;

            var node = BootstrapNode;
            if (node != null) setup.WithBootstrapNode(node);
        }
    }
}
