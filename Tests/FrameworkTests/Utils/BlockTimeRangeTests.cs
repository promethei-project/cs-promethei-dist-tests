using PrometheiContractsPlugin.ChainMonitor;
using NUnit.Framework;
using Utils;

namespace FrameworkTests.Utils
{
    [TestFixture]
    public class BlockTimeRangeTests
    {
        [Test]
        public void BlockTimeGetter1()
        {
            ulong from = 201700912;
            ulong to = 201704510;
            var timeRange = new TimeRange(
                from: new DateTime(2025, 10, 6, 7, 31, 54),
                to:   new DateTime(2025, 10, 6, 7, 46, 54)
            );

            var interval = new BlockInterval(timeRange, from, to);
            var getter = new ChainState.BlockTimeGetter(interval);

            var numBlocks = to - from;
            var spanPerBlock = timeRange.Duration / numBlocks;

            var steps = 0;
            for (ulong i = from; i <= to; i++)
            {
                var expectedUtc = timeRange.From + (spanPerBlock * steps);

                var entry = getter.Get(i);
                Assert.That(entry.Utc, Is.EqualTo(expectedUtc).Within(TimeSpan.FromSeconds(0.5)));

                steps++;
            }
        }
    }
}
