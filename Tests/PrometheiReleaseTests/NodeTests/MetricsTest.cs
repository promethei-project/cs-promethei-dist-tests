using PrometheiTests;
using MetricsPlugin;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.NodeTests
{
    [TestFixture]
    public class MetricsTest : AutoBootstrapDistTest
    {
        [Test]
        public void BasicMetrics()
        {
            var nodes = StartPromethei(2, s => s.EnableMetrics());
            
            var metrics = Ci.GetMetricsFor(scrapeInterval: TimeSpan.FromSeconds(10), nodes);

            nodes[0].DownloadContent(nodes[1].UploadFile(GenerateTestFile(3.MB())));
            
            metrics[0].AssertThat("libp2p_peers", Is.EqualTo(1));
            metrics[1].AssertThat("libp2p_peers", Is.EqualTo(1));

            metrics[0].AssertThat("promethei_block_exchange_want_block_lists_sent_total", Is.GreaterThanOrEqualTo(2));
            metrics[1].AssertThat("promethei_block_exchange_want_block_lists_received_total", Is.GreaterThanOrEqualTo(2));

            metrics[0].AssertThat("dht_message_requests_incoming_total", Is.GreaterThan(1));
            metrics[0].AssertThat("dht_message_requests_outgoing_total", Is.GreaterThan(1));
            metrics[1].AssertThat("dht_message_requests_incoming_total", Is.GreaterThan(1));
            metrics[1].AssertThat("dht_message_requests_outgoing_total", Is.GreaterThan(1));
        }
    }
}
