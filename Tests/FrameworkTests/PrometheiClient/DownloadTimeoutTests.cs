using PrometheiClient;
using NUnit.Framework;

namespace FrameworkTests.PrometheiClient
{
    [TestFixture]
    public class DownloadTimeoutTests
    {
        private const string EnvName = "PROMETHEI_DOWNLOAD_TIMEOUT_MINUTES";
        private string? previousValue;

        [SetUp]
        public void SetUp()
        {
            previousValue = Environment.GetEnvironmentVariable(EnvName);
        }

        [TearDown]
        public void TearDown()
        {
            if (previousValue == null) Environment.SetEnvironmentVariable(EnvName, null);
            else Environment.SetEnvironmentVariable(EnvName, previousValue);
        }

        [Test]
        public void ValidValueIsUsed()
        {
            Environment.SetEnvironmentVariable(EnvName, "120");

            Assert.That(PrometheiNode.ParseDefaultDownloadTimeout(), Is.EqualTo(TimeSpan.FromMinutes(120)));
        }

        [Test]
        public void MalformedValueFallsBackToDefault()
        {
            Environment.SetEnvironmentVariable(EnvName, "not-a-number");

            Assert.That(PrometheiNode.ParseDefaultDownloadTimeout(), Is.EqualTo(TimeSpan.FromMinutes(45)));
        }

        [Test]
        public void ZeroFallsBackToDefault()
        {
            Environment.SetEnvironmentVariable(EnvName, "0");

            Assert.That(PrometheiNode.ParseDefaultDownloadTimeout(), Is.EqualTo(TimeSpan.FromMinutes(45)));
        }

        [Test]
        public void NegativeFallsBackToDefault()
        {
            Environment.SetEnvironmentVariable(EnvName, "-5");

            Assert.That(PrometheiNode.ParseDefaultDownloadTimeout(), Is.EqualTo(TimeSpan.FromMinutes(45)));
        }

        [Test]
        public void UnsetFallsBackToDefault()
        {
            Environment.SetEnvironmentVariable(EnvName, null);

            Assert.That(PrometheiNode.ParseDefaultDownloadTimeout(), Is.EqualTo(TimeSpan.FromMinutes(45)));
        }
    }
}
