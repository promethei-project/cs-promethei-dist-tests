using NUnit.Framework;

namespace ExperimentalTests.DownloadConnectivityTests
{
    [TestFixture]
    public class AvailableDownloadersTests
    {
        [Test]
        public void AssignFillsPlanToRequestedSize()
        {
            var available = new DatalayerReliabilityTests.AvailableDownloaders(5, 30);
            var plan = new DatalayerReliabilityTests.TransferPlan();

            available.Assign(plan);

            Assert.That(plan.Downloaders, Has.Count.EqualTo(30));
        }

        [Test]
        public void NoDownloaderExceedsMaxUsage()
        {
            var available = new DatalayerReliabilityTests.AvailableDownloaders(5, 30);
            var plan = new DatalayerReliabilityTests.TransferPlan();

            available.Assign(plan);

            Assert.That(available.GetAll().All(d => d.TransferPlans.Count <= 5), Is.True);
        }

        [Test]
        public void AllCreatedDownloadersAreUsed()
        {
            var available = new DatalayerReliabilityTests.AvailableDownloaders(5, 30);
            var plan = new DatalayerReliabilityTests.TransferPlan();

            available.Assign(plan);

            Assert.That(available.GetAll().All(d => d.TransferPlans.Count > 0), Is.True);
        }

        [Test]
        public void MultiplePlansDistributeWithinCap()
        {
            var available = new DatalayerReliabilityTests.AvailableDownloaders(2, 4);
            var plans = new[]
            {
                new DatalayerReliabilityTests.TransferPlan(),
                new DatalayerReliabilityTests.TransferPlan(),
                new DatalayerReliabilityTests.TransferPlan(),
            };

            foreach (var plan in plans)
            {
                available.Assign(plan);
            }

            Assert.That(available.GetAll().All(d => d.TransferPlans.Count <= 2), Is.True);
            Assert.That(available.GetAll().Sum(d => d.TransferPlans.Count), Is.EqualTo(12));
            // Across plans the cap binds: the first batch of downloaders
            // reaches exactly maxUsagePerDownloader and is retired.
            Assert.That(available.GetAll().Any(d => d.TransferPlans.Count == 2), Is.True);
        }

        [Test]
        public void SingleUsagePerDownloader()
        {
            var available = new DatalayerReliabilityTests.AvailableDownloaders(1, 3);
            var plan = new DatalayerReliabilityTests.TransferPlan();

            available.Assign(plan);

            Assert.That(plan.Downloaders, Has.Count.EqualTo(3));
            Assert.That(available.GetAll().Length, Is.EqualTo(3));
            Assert.That(available.GetAll().All(d => d.TransferPlans.Count == 1), Is.True);
        }
    }
}
