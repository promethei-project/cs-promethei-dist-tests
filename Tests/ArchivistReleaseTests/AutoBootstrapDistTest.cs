using ArchivistClient;
using ArchivistPlugin;
using NUnit.Framework;

namespace ArchivistTests
{
    public class AutoBootstrapDistTest : ArchivistDistTest
    {
        private bool isBooting = false;

        public IArchivistNode BootstrapNode { get; private set; } = null!;

        [SetUp]
        public void SetupBootstrapNode()
        {
            isBooting = true;
            BootstrapNode = StartArchivist(s =>
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

        protected virtual void OnBootstrapNodeSetup(IArchivistSetup setup)
        {
        }

        protected override void OnArchivistSetup(IArchivistSetup setup)
        {
            if (isBooting) return;

            var node = BootstrapNode;
            if (node != null) setup.WithBootstrapNode(node);
        }
    }
}
