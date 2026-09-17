using PrometheiTests;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.DataTests
{
    public class ThreeClientTest : AutoBootstrapDistTest
    {
        [Test]
        public void ThreeClient()
        {
            var primary = StartPromethei();
            var secondary = StartPromethei();

            var testFile = GenerateTestFile(10.MB());

            var contentId = primary.UploadFile(testFile);
            AssertNodesContainFile(contentId, primary);

            var downloadedFile = secondary.DownloadContent(contentId);
            AssertNodesContainFile(contentId, primary, secondary);

            testFile.AssertIsEqual(downloadedFile);
        }
    }
}
