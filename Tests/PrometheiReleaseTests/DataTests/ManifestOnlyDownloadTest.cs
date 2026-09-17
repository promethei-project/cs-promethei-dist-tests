using PrometheiTests;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.DataTests
{
    [TestFixture]
    public class ManifestOnlyDownloadTest : PrometheiDistTest
    {
        [Test]
        public void ManifestOnlyTest()
        {
            var uploader = StartPromethei();
            var downloader = StartPromethei(s => s.WithBootstrapNode(uploader));

            var file = GenerateTestFile(2.GB());
            var size = file.GetFilesize().SizeInBytes;
            var cid = uploader.UploadFile(file);

            var startSpace = downloader.Space();
            var localDataset = downloader.DownloadManifestOnly(cid);

            Sleep(TimeSpan.FromSeconds(3));

            var spaceDiff = startSpace.FreeBytes - downloader.Space().FreeBytes;

            Assert.That(spaceDiff, Is.LessThan(512.KB().SizeInBytes));

            Assert.That(localDataset.Cid, Is.EqualTo(cid));
            Assert.That(localDataset.Manifest.DatasetSize.SizeInBytes, Is.EqualTo(file.GetFilesize().SizeInBytes));
        }
    }
}
