using PrometheiTests;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.NodeTests
{
    [TestFixture]
    public class BasicInfoTests : PrometheiDistTest
    {
        [Test]
        public void QuotaTest()
        {
            var size = 3.GB();
            var node = StartPromethei(s => s.WithStorageQuota(size));
            var space = node.Space();

            Assert.That(space.QuotaMaxBytes, Is.EqualTo(size.SizeInBytes));
        }

        [Test]
        public void Spr()
        {
            var node = StartPromethei();
            
            var info = node.GetDebugInfo();
            Assert.That(!string.IsNullOrEmpty(info.Spr));

            var spr = node.GetSpr();
            Assert.That(!string.IsNullOrEmpty(spr));

            Assert.That(info.Spr, Is.EqualTo(spr));
        }

        [Test]
        public void VersionInfo()
        {
            var node = StartPromethei();

            var info = node.GetDebugInfo();
            Assert.That(!string.IsNullOrEmpty(info.Version.Version));
            Assert.That(!string.IsNullOrEmpty(info.Version.Revision));
        }

        [Test]
        public void AnnounceAddress()
        {
            var node = StartPromethei();
            var addr = node.GetListenEndpoint();

            var info = node.GetDebugInfo();

            Assert.That(info.AnnounceAddresses.Count, Is.GreaterThan(0));
            // Ideally we'd assert the pod IP is in the announce address, but we can't access it from here.
        }
    }
}
