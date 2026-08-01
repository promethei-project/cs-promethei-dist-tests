using KubernetesWorkflow;
using Logging;
using NUnit.Framework;

namespace FrameworkTests.KubernetesWorkflow
{
    [TestFixture]
    public class ContainerLogFollowerTests
    {
        private string tempDir = null!;
        private TestLog testLog = null!;
        private TestHandler handler = null!;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "follower-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var file = Path.Combine(tempDir, "follow.log");
            testLog = new TestLog(file);
            handler = new TestHandler(testLog, "test follow", "follow");
        }

        [TearDown]
        public void TearDown()
        {
            handler.Dispose();
            Directory.Delete(tempDir, true);
        }

        [Test]
        public void LinesAreWrittenToFile()
        {
            handler.Feed("hello world");

            Assert.That(ReadLog(), Does.Contain("hello world"));
        }

        [Test]
        public void TimestampPrefixIsStripped()
        {
            handler.Feed("2026-07-31T12:00:00.000000000Z hello world");

            var log = ReadLog();
            Assert.That(log, Does.Contain("hello world"));
            Assert.That(log, Does.Not.Contain("2026-07-31T12:00:00"));
        }

        [Test]
        public void LastTimestampAdvancesMonotonically()
        {
            handler.Feed("2026-07-31T12:00:00.000000000Z first");
            handler.Feed("2026-07-31T12:00:05.000000000Z second");
            handler.Feed("2026-07-31T12:00:01.000000000Z older");

            Assert.That(handler.LastTimestamp,
                Is.EqualTo(new DateTimeOffset(2026, 7, 31, 12, 0, 5, TimeSpan.Zero)));
        }

        [Test]
        public void ExactRepeatsAreSuppressed()
        {
            handler.Feed("2026-07-31T12:00:00.000000000Z duplicate");
            handler.Feed("2026-07-31T12:00:00.000000000Z duplicate");

            Assert.That(handler.LinesWritten, Is.EqualTo(1));
            Assert.That(CountOccurrences(ReadLog(), "duplicate"), Is.EqualTo(1));
        }

        [Test]
        public void DedupIsKeyedOnRawLineIncludingTimestamp()
        {
            // Suppression is NEVER based on timestamp alone: the same message
            // text at two different timestamps is two distinct writes and must
            // both be captured.
            handler.Feed("2026-07-31T12:00:00.000000000Z repeated");
            handler.Feed("2026-07-31T12:00:01.000000000Z repeated");

            Assert.That(handler.LinesWritten, Is.EqualTo(2));
        }

        [Test]
        public void DedupWindowSuppressesLinesStillInsideWindow()
        {
            for (var i = 0; i < 4096; i++)
            {
                handler.Feed($"line-{i}");
            }
            // Exactly 4096 distinct lines fill the window: line-0 is still
            // present, so re-feeding it is suppressed.
            handler.Feed("line-0");

            Assert.That(handler.LinesWritten, Is.EqualTo(4096));
        }

        [Test]
        public void DedupWindowEvictsOldLines()
        {
            for (var i = 0; i < 4097; i++)
            {
                handler.Feed($"line-{i}");
            }
            // The 4097th write evicted line-0, so it is written again.
            handler.Feed("line-0");
            // line-4096 is still inside the window, so it is suppressed.
            handler.Feed("line-4096");

            Assert.That(handler.LinesWritten, Is.EqualTo(4098));
        }

        [Test]
        public void FilteredLinesAreDropped()
        {
            handler.Feed("Received JSON-RPC response");
            handler.Feed("Received JSON-RPC response topics=foo");
            handler.Feed("object field not marked with serialize, skipping");

            Assert.That(handler.LinesWritten, Is.EqualTo(1));
            Assert.That(ReadLog(), Does.Contain("topics=foo"));
        }

        [Test]
        public void StringReplaceIsApplied()
        {
            handler.Feed("value secret");

            Assert.That(ReadLog(), Does.Contain("REDACTED"));
        }

        private string ReadLog()
        {
            return File.ReadAllText(testLog.Filename);
        }

        private static int CountOccurrences(string text, string needle)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }

        // Minimal ILog that writes into the test temp directory and applies a
        // fixed string replacement, so assertions can verify the replacement
        // path without touching real log locations.
        private class TestLog : ILog
        {
            public TestLog(string filename)
            {
                Filename = filename;
            }

            public string Filename { get; }

            public void Log(string message) { }
            public void Debug(string message = "", int skipFrames = 0) { }
            public void Error(string message) { }
            public void Raw(string message) { }
            public void AddStringReplace(string from, string to) { }

            public string ApplyStringReplace(string str)
            {
                return str.Replace("secret", "REDACTED");
            }

            public LogFile CreateSubfile(string addName, string ext = "log")
            {
                return new LogFile(Filename);
            }

            public string GetFullName()
            {
                return "test";
            }
        }

        private class TestHandler : ContainerLogFollower.BufferedFollowLogHandler
        {
            public TestHandler(ILog sourceLog, string description, string addFileName)
                : base(sourceLog, description, addFileName)
            {
            }

            public void Feed(string line)
            {
                ProcessLine(line);
            }
        }
    }
}
