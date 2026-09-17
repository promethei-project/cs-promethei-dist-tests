using PrometheiClient;
using PrometheiTests;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.DataTests
{
    [TestFixture]
    public class DataRestartTest : AutoBootstrapDistTest
    {
        [Test]
        public void KeepsData()
        {
            var node = StartPromethei();

            var file = GenerateTestFile(10.MB());
            var cid = node.UploadFile(file);
            var initialSpace = node.Space();

            node.InPlaceRestart();

            AssertEqualSpace(node, initialSpace);
            var download1 = node.DownloadContent(cid);

            node.InPlaceRestart();

            AssertEqualSpace(node, initialSpace);
            var download2 = node.DownloadContent(cid);

            file.AssertIsEqual(download1);
            file.AssertIsEqual(download2);
        }

        [Test]
        public void SendAfterRestart()
        {
            var uploader = StartPromethei(s => s.WithName("restart_uploader"));
            var downloader = StartPromethei(s => s.WithName("downloader"));

            var file = GenerateTestFile(10.MB());
            var cid = uploader.UploadFile(file);

            uploader.InPlaceRestart();

            var downloaded = downloader.DownloadContent(cid);

            file.AssertIsEqual(downloaded);
        }

        [Test]
        public void ReceiveAfterRestart()
        {
            var uploader = StartPromethei(s => s.WithName("uploader"));
            var downloader = StartPromethei(s => s.WithName("restart_downloader"));

            var file = GenerateTestFile(10.MB());
            var cid = uploader.UploadFile(file);

            downloader.InPlaceRestart();

            var downloaded = downloader.DownloadContent(cid);

            file.AssertIsEqual(downloaded);
        }

        private void AssertEqualSpace(IPrometheiNode node, PrometheiSpace expected)
        {
            var actual = node.Space();

            Assert.That(actual.QuotaMaxBytes, Is.EqualTo(expected.QuotaMaxBytes));
            Assert.That(actual.TotalBlocks, Is.EqualTo(expected.TotalBlocks));
            Assert.That(actual.QuotaUsedBytes, Is.EqualTo(expected.QuotaUsedBytes));
            Assert.That(actual.QuotaReservedBytes, Is.EqualTo(expected.QuotaReservedBytes));
        }
    }
}
