using PrometheiClient;
using PrometheiTests;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.DataTests
{
    [TestFixture]
    public class OneClientTest : PrometheiDistTest
    {
        [Test]
        public void OneClient()
        {
            var node = StartPromethei();

            PerformOneClientTest(node);

            LogNodeStatus(node);
        }

        [Test]
        public void UploadQuotaLimit()
        {
            var node = StartPromethei(n => n.WithStorageQuota(10.MB()));

            for (var i = 0; i < 9; i++)
            {
                node.UploadFile(GenerateTestFile(1.MB()));
                node.Space();
            }

            try
            {
                node.UploadFile(GenerateTestFile(1.MB()));
                node.Space();
                Assert.Fail("Successfully uploaded a dataset causing the node to exceed its quota.");
            }
            catch (Exception ex)
            {
                Assert.That(ex.Message.Contains("413"),
                    $"Expected HTTP 413 error, but got: {ex.Message}");
                Log("Upload failed with correct HTTP 413 status code.");
            }
        }

        private void PerformOneClientTest(IPrometheiNode primary)
        {
            var filesize = 1.MB();
            var testFile = GenerateTestFile(filesize);

            var contentType = "video/mp4";
            var filename = "testFilename";
            var cid = primary.UploadFile(testFile, contentType, filename);

            AssertNodesContainFile(cid, primary);

            var manifest = primary.DownloadManifestOnly(cid).Manifest;
            Assert.That(manifest.Filename, Is.EqualTo(filename));
            Assert.That(manifest.Mimetype, Is.EqualTo(contentType));

            var downloadedFile = primary.DownloadContent(cid);
            testFile.AssertIsEqual(downloadedFile);

            var space = primary.Space();
            Assert.That(space.QuotaUsedBytes, Is.EqualTo(filesize.SizeInBytes + 83)); // manifest size?
            Assert.That(space.TotalBlocks, Is.EqualTo(manifest.NumBlocks + 1));

            var status = primary.GetDatasetStatus(cid);
            Assert.Multiple(() =>
            {
                Assert.That(status.Cid.Id, Is.EqualTo(cid.Id));
                Assert.That(status.State, Is.EqualTo(DatasetStatusState.Completed));
                Assert.That(status.ExpiryUtc, Is.EqualTo(DateTime.UtcNow + TimeSpan.FromHours(24 * 30)).Within(TimeSpan.FromMinutes(30)));
                Assert.That(status.Blocks.Length, Is.EqualTo(manifest.NumBlocks));
                Assert.That(status.Blocks.IsFullySet());
            });
        }
    }
}
