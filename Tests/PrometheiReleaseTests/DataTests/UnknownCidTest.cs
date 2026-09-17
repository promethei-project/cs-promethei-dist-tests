using PrometheiClient;
using PrometheiTests;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.DataTests
{
    [TestFixture]
    public class UnknownCidTest : PrometheiDistTest
    {
        [Test]
        public void DownloadingUnknownCidDoesNotCauseCrash()
        {
            var node = StartPromethei();

            var unknownCid = new ContentId("zDvZRwzkzHsok3Z8yMoiXE9EDBFwgr8WygB8s4ddcLzzSwwXAxLZ", 1.GB());

            var localFiles = node.LocalFiles().Content;
            Assert.That(localFiles.Select(f => f.Cid), Does.Not.Contain(unknownCid));

            try
            {
                node.DownloadContent(unknownCid, TimeSpan.FromMinutes(2.0));
            }
            catch (Exception ex)
            {
                var expectedMessage = $"Download of '{unknownCid.Id}' timed out";
                if (!ex.Message.StartsWith(expectedMessage)) throw;
            }

            WaitAndCheckNodesStaysAlive(TimeSpan.FromMinutes(2), node);
        }
    }
}
