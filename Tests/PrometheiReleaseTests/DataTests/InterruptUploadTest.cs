using PrometheiClient;
using PrometheiTests;
using FileUtils;
using NUnit.Framework;
using System.Diagnostics;
using Utils;

namespace PrometheiReleaseTests.DataTests
{
    public class InterruptUploadTest : PrometheiDistTest
    {
        [Test]
        public void UploadInterruptTest()
        {
            var nodes = StartPromethei(10);

            var tasks = nodes.Select(n => Task<bool>.Run(() => RunInterruptUploadTest(n)));
            Task.WaitAll(tasks.ToArray());

            Log("We expect no crashes to be visible in the node logs.");
            Assert.That(tasks.Select(t => t.Result).All(r => r == true));

            WaitAndCheckNodesStaysAlive(TimeSpan.FromMinutes(2), nodes);
        }

        private bool RunInterruptUploadTest(IPrometheiNode node)
        {
            Log("Starting upload, then interrupting it...");
            var file = GenerateTestFile(300.MB());

            var process = StartCurlUploadProcess(node, file);

            Thread.Sleep(500);
            process.Kill();
            Thread.Sleep(1000);

            var log = node.DownloadLog();
            return !log.GetLinesContaining("Unhandled exception in async proc, aborting").Any();
        }

        private Process StartCurlUploadProcess(IPrometheiNode node, TrackedFile file)
        {
            var apiAddress = node.GetApiEndpoint();
            var prometheiUrl = $"{apiAddress}/api/promethei/v1/data";
            var filePath = file.Filename;
            return Process.Start("curl", $"-X POST {prometheiUrl} -H \"Content-Type: application/octet-stream\" -T {filePath}");
        }
    }
}
