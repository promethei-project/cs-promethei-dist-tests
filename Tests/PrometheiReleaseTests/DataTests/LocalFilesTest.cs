using PrometheiTests;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.DataTests
{
    [TestFixture]
    public class LocalFilesTest : PrometheiDistTest
    {
        [Test]
        public void ShouldShowLocalFiles()
        {
            var node = StartPromethei();

            var size1 = 123.KB();
            var size2 = 23.MB();
            var file1 = GenerateTestFile(size1);
            var file2 = GenerateTestFile(size2);

            var cid1 = node.UploadFile(file1);
            var cid2 = node.UploadFile(file2);

            var localFiles = node.LocalFiles();

            Assert.That(localFiles.Content.Length, Is.EqualTo(2));

            var local1 = localFiles.Content.Single(f => f.Cid == cid1);
            var local2 = localFiles.Content.Single(f => f.Cid == cid2);

            Assert.That(local1.Manifest.Protected, Is.False);
            Assert.That(local1.Manifest.DatasetSize, Is.EqualTo(size1));
            Assert.That(local2.Manifest.Protected, Is.False);
            Assert.That(local2.Manifest.DatasetSize, Is.EqualTo(size2));
        }
    }
}
