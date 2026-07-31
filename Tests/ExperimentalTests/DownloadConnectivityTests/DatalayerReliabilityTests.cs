using ArchivistClient;
using ArchivistPlugin;
using ArchivistTests;
using FileUtils;
using KubernetesWorkflow;
using NUnit.Framework;
using Utils;

namespace ExperimentalTests.DownloadConnectivityTests
{
    /// <summary>
    /// https://hackmd.io/rwPtPJ7KTw6cGjhN0zNYig
    /// </summary>
    [TestFixture]
    public class DatalayerReliabilityTests : AutoBootstrapDistTest
    {
        [Test]
        [TestCase(1000, 1)]
        [TestCase(1000, 3)]
        [TestCase(1000, 5)]
        [TestCase(1000, 10)]
        [TestCase(1000, 20)]
        [TestCase(1000, 30)]
        [TestCase(5000, 1)]
        [TestCase(5000, 3)]
        [TestCase(5000, 5)]
        [TestCase(5000, 10)]
        [TestCase(5000, 20)]
        [TestCase(5000, 30)]
        public async Task SingleSetTest(
            int fileSizeMb,
            int numDownloaders
        )
        {
            // Memory-aware spread over the local two-node k3s cluster:
            // server1 (~23GB avail) carries the test runner (16Gi request) and
            // the bootstrap node; server2 (~14GB avail, desktop residents)
            // carries the uploader (0.36GB x peers) plus its downloader share.
            // Split fraction is settable via ARCHIVIST_SERVER1_FRACTION (default
            // 0.5 = even by count) so placement can be sized to measured node
            // budgets without rebuilding the harness image.
            // Both named locations are REQUIRED for this experiment; fail fast
            // rather than risk a packed single-node run.
            var loc1 = RequireLocation("ubuntu-server1");
            var loc2 = RequireLocation("ubuntu-server2");
            var server1Fraction = double.Parse(EnvVar.GetOrDefault("ARCHIVIST_SERVER1_FRACTION", "0.5"));

            var file = GenerateTestFile(fileSizeMb.MB());
            var uploader = StartArchivist(n =>
            {
                SetupArchivist(n, "uploader");
                n.At(loc1);
            });

            var onServer1 = (int)Math.Round(numDownloaders * server1Fraction);
            if (numDownloaders > 1 && onServer1 > 0 && onServer1 < numDownloaders)
            {
                var group1 = StartArchivist(onServer1, n =>
                {
                    SetupArchivist(n, "downloader");
                    n.At(loc1);
                });
                var group2 = StartArchivist(numDownloaders - onServer1, n =>
                {
                    SetupArchivist(n, "downloader");
                    n.At(loc2);
                });
                var downloaders = group1.Concat(group2).ToArray();
                await RunDownloads(uploader, file, downloaders);
            }
            else
            {
                var downloaders = StartArchivist(numDownloaders, n =>
                {
                    SetupArchivist(n, "downloader");
                    n.At(loc1);
                }).ToArray();
                await RunDownloads(uploader, file, downloaders);
            }
        }

        private ILocation RequireLocation(string kubeNodeName)
        {
            var locations = Ci.GetKnownLocations();
            Assert.GreaterOrEqual(locations.NumberOfLocations, 2,
                "Spread run requires both k3s nodes to be available as locations.");
            return locations.Get(kubeNodeName);
        }

        private static void SetupArchivist(IArchivistSetup n, string name)
        {
            // Log-level A/B knob: allow overriding BlockExchange/DiscV5 topic
            // level to Warn to separate tracing overhead from copy cost in
            // throughput measurements.
            var topicLevel = EnvVar.GetOrDefault("ARCHIVIST_TOPIC_LOG_LEVEL", "Trace") == "Warn"
                ? ArchivistLogLevel.Warn : ArchivistLogLevel.Trace;
            n.WithName(name).WithLogLevel(ArchivistLogLevel.Warn,
                new ArchivistLogCustomTopics(topicLevel, ArchivistLogLevel.Warn, topicLevel));
            // Fetch-order A/B knob: applies to downloaders (the fetch side)
            // only; the uploader never fetches in this test.
            var fetchOrder = EnvVar.GetOrDefault("ARCHIVIST_FETCH_ORDER", "");
            if (!string.IsNullOrEmpty(fetchOrder) && name == "downloader")
            {
                n.WithFetchOrder(fetchOrder);
            }
        }

        private static async Task RunDownloads(IArchivistNode uploader, TrackedFile file, IArchivistNode[] downloaders)
        {
            var cid = uploader.UploadFile(file);

            var downloadTasks = downloaders
                .Select(dl => dl.DownloadContentAsync(cid))
                .ToArray();

            await Task.WhenAll(downloadTasks);

            Assert.That(downloadTasks.All(t => !t.IsFaulted));
        }

        protected override void OnBootstrapNodeSetup(IArchivistSetup setup)
        {
            setup.At(RequireLocation("ubuntu-server1"));
        }

        public class TransferPlan
        {
            public IArchivistNode Uploader { get; set; } = null!;
            public ContentId Cid { get; set; } = null!;
            public List<DownloaderPlan> Downloaders { get; } = new List<DownloaderPlan>();
        }

        public class DownloaderPlan
        {
            public IArchivistNode Node { get; set; } = null!;
            public List<TransferPlan> TransferPlans { get; } = new List<TransferPlan>();
        }

        public class AvailableDownloaders
        {
            private readonly List<DownloaderPlan> all = new List<DownloaderPlan>();
            private readonly List<DownloaderPlan> available = new List<DownloaderPlan>();
            private readonly int maxUsagePerDownloader;
            private readonly int numDownloadersPerPlan;

            public AvailableDownloaders(int maxUsagePerDownloader, int numDownloadersPerPlan)
            {
                this.maxUsagePerDownloader = maxUsagePerDownloader;
                this.numDownloadersPerPlan = numDownloadersPerPlan;
            }

            public void Assign(TransferPlan plan)
            {
                while (plan.Downloaders.Count < numDownloadersPerPlan)
                {
                    var open = available.Where(a => !plan.Downloaders.Contains(a)).ToArray();
                    if (!open.Any())
                    {
                        var dl = new DownloaderPlan();
                        all.Add(dl);
                        available.Add(dl);
                    }
                    else
                    {
                        var dl = RandomUtils.GetOneRandom(open);
                        dl.TransferPlans.Add(plan);
                        plan.Downloaders.Add(dl);

                        if (dl.TransferPlans.Count == maxUsagePerDownloader) available.Remove(dl);
                    }
                }
            }

            public DownloaderPlan[] GetAll()
            {
                return all.ToArray();
            }
        }

        [Test]
        [Combinatorial]
        public async Task MultiSetTest(
            [Values(3, 10)] int numDatasets,
            [Values(5000, 100)] int fileSizeMb,
            [Values(30)] int numDownloadersPerDataset,
            [Values(5)] int maxDatasetsPerDownloader
        )
        {
            var loc1 = RequireLocation("ubuntu-server1");
            var loc2 = RequireLocation("ubuntu-server2");
            var server1Fraction = double.Parse(EnvVar.GetOrDefault("ARCHIVIST_SERVER1_FRACTION", "0.5"));

            var plans = new List<TransferPlan>();
            var onServer1 = Math.Min(Math.Max((int)Math.Round(numDatasets * server1Fraction), 1), numDatasets - 1);
            var onServer2 = numDatasets - onServer1;
            var group1Uploaders = StartArchivist(onServer1, n =>
            {
                SetupArchivist(n, "uploader");
                n.At(loc1);
            });
            var group2Uploaders = StartArchivist(onServer2, n =>
            {
                SetupArchivist(n, "uploader");
                n.At(loc2);
            });
            var uploaders = group1Uploaders.Concat(group2Uploaders).ToArray();
            foreach (var n in uploaders)
            {
                plans.Add(new TransferPlan
                {
                    Uploader = n,
                    Cid = n.UploadFile(GenerateTestFile(fileSizeMb.MB()))
                });
            }

            Assert.That(plans.Count, Is.GreaterThan(0));

            var available = new AvailableDownloaders(maxDatasetsPerDownloader, numDownloadersPerDataset);
            foreach (var plan in plans)
            {
                available.Assign(plan);
            }
            var allDownloaderPlans = available.GetAll();
            Assert.That(allDownloaderPlans.Length, Is.LessThan(100));
            Log($"Using {allDownloaderPlans.Length} downloaders...");

            var dlOnServer1 = (int)Math.Round(allDownloaderPlans.Length * server1Fraction);
            var dlOnServer2 = allDownloaderPlans.Length - dlOnServer1;
            var dlGroup1 = StartArchivist(dlOnServer1, n =>
            {
                SetupArchivist(n, "downloader");
                n.At(loc1);
            });
            var dlGroup2 = StartArchivist(dlOnServer2, n =>
            {
                SetupArchivist(n, "downloader");
                n.At(loc2);
            });
            var nodes = dlGroup1.Concat(dlGroup2).ToArray();

            for (var i = 0; i < allDownloaderPlans.Length; i++)
            {
                allDownloaderPlans[i].Node = nodes[i];
            }

            var downloadTasks = allDownloaderPlans.Select(async dlPlan =>
            {
                var tf = dlPlan.TransferPlans.ToList();
                while (tf.Count > 0)
                {
                    var t = tf.PickOneRandom();
                    await dlPlan.Node.DownloadContentAsync(t.Cid);
                }
            }).ToArray();

            await Task.WhenAll(downloadTasks);

            Assert.That(downloadTasks.All(t => !t.IsFaulted));
        }
    }
}
