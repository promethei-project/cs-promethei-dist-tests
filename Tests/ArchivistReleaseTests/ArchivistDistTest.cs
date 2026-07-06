using BlockchainUtils;
using ArchivistClient;
using ArchivistContractsPlugin;
using ArchivistPlugin;
using ArchivistPlugin.OverwatchSupport;
using ArchivistTests.Helpers;
using Core;
using DistTestCore;
using DistTestCore.Helpers;
using DistTestCore.Logs;
using GethPlugin;
using Logging;
using MetricsPlugin;
using Newtonsoft.Json;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using OverwatchTranscript;
using Utils;

namespace ArchivistTests
{
    public class ArchivistDistTest : DistTest
    {
        private readonly BlockCache blockCache = new BlockCache(new NullLog());
        private readonly List<IArchivistNode> nodes = new List<IArchivistNode>();
        private ArchivistTranscriptWriter? writer;

        public ArchivistDistTest()
        {
            ProjectPlugin.Load<ArchivistPlugin.ArchivistPlugin>();
            ProjectPlugin.Load<ArchivistContractsPlugin.ArchivistContractsPlugin>();
            ProjectPlugin.Load<GethPlugin.GethPlugin>();
            ProjectPlugin.Load<MetricsPlugin.MetricsPlugin>();
        }

        [SetUp]
        public void SetupArchivistDistTest()
        {
            writer = SetupTranscript();
        }

        [TearDown]
        public void TearDownArchivistDistTest()
        {
            TeardownTranscript();
        }

        protected override void Initialize(FixtureLog fixtureLog)
        {
            Ci.AddArchivistHooksProvider(new ArchivistLogTrackerProvider(nodes.Add));
        }

        public IArchivistNode StartArchivist()
        {
            return StartArchivist(s => { });
        }

        public IArchivistNode StartArchivist(Action<IArchivistSetup> setup)
        {
            return StartArchivist(1, setup)[0];
        }

        public IArchivistNodeGroup StartArchivist(int numberOfNodes)
        {
            return StartArchivist(numberOfNodes, s => { });
        }

        public IArchivistNodeGroup StartArchivist(int numberOfNodes, Action<IArchivistSetup> setup)
        {
            var group = Ci.StartArchivistNodes(numberOfNodes, s =>
            {
                setup(s);
                OnArchivistSetup(s);
            });

            return group;
        }

        public IGethNode StartGethNode(Action<IGethSetup> setup)
        {
            return Ci.StartGethNode(blockCache, setup);
        }

        public PeerConnectionTestHelpers CreatePeerConnectionTestHelpers(int numTries = 2)
        {
            return new PeerConnectionTestHelpers(GetTestLog(), numTries);
        }

        public PeerDownloadTestHelpers CreatePeerDownloadTestHelpers(TimeSpan downloadTimeout, int numTries = 2)
        {
            return new PeerDownloadTestHelpers(GetTestLog(), numTries, GetFileManager(), downloadTimeout);
        }

        public void AssertBalance(IArchivistContracts contracts, IArchivistNode archivistNode, Constraint constraint, string msg)
        {
            Assert.Fail("Depricated, use MarketplaceAutobootstrapDistTest assertBalances instead.");
            AssertHelpers.RetryAssert(constraint, () => contracts.GetTestTokenBalance(archivistNode), nameof(AssertBalance) + msg);
        }

        public void CheckLogForErrors(params IArchivistNode[] nodes)
        {
            foreach (var node in nodes) CheckLogForErrors(node);
        }

        public void CheckLogForErrors(IArchivistNode node)
        {
            Log($"Checking {node.GetName()} log for errors.");
            var log = node.DownloadLog();

            log.AssertLogDoesNotContain("Block validation failed");
            log.AssertLogDoesNotContainLinesStartingWith("ERR ");
        }

        public void LogNodeStatus(IArchivistNode node, IMetricsAccess? metrics = null)
        {
            Log("Status for " + node.GetName() + Environment.NewLine +
                GetBasicNodeStatus(node));
        }

        public void WaitAndCheckNodesStaysAlive(TimeSpan duration, IArchivistNodeGroup nodes)
        {
            WaitAndCheckNodesStaysAlive(duration, nodes.ToArray());
        }

        public void WaitAndCheckNodesStaysAlive(TimeSpan duration, List<IArchivistNode> nodes)
        {
            WaitAndCheckNodesStaysAlive(duration, nodes.ToArray());
        }

        public void WaitAndCheckNodesStaysAlive(TimeSpan duration, params IArchivistNode[] nodes)
        {
            WaitAndCheck(duration,
                loopTime: TimeSpan.FromSeconds(3.0),
                check: () =>
                {
                    foreach (var node in nodes)
                    {
                        Assert.That(node.HasCrashed(), Is.False, $"Node {node.GetName()} has crashed.");

                        var info = node.GetDebugInfo();
                        Assert.That(!string.IsNullOrEmpty(info.Id), $"Node {node.GetName()} failed to respond to debug/info call.");
                    }
                },
                skipFrames: 1);
        }

        public void WaitAndCheck(TimeSpan duration, TimeSpan loopTime, Action check, int skipFrames = 0)
        {
            Log($"{Time.FormatDuration(duration)}...", 1 + skipFrames);

            Assert.That(duration.TotalSeconds, Is.GreaterThan(loopTime.TotalSeconds));

            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start) < duration)
            {
                Thread.Sleep(loopTime);
                check();
            }

            Log($"OK", 1 + skipFrames);
        }

        public void AssertNodesContainFile(ContentId cid, IArchivistNodeGroup nodes)
        {
            AssertNodesContainFile(cid, nodes.ToArray());
        }

        public void AssertNodesContainFile(ContentId cid, params IArchivistNode[] nodes)
        {
            Log($"{nodes.Names()} {cid}...");

            foreach (var node in nodes)
            {
                var localDatasets = node.LocalFiles();
                Assert.That(localDatasets.Content.Select(c => c.Cid), Has.One.EqualTo(cid));

                var dataset = node.GetDatasetStatus(cid);
                Assert.That(dataset.Blocks.IsFullySet());
            }

            Log("OK");
        }

        public void AssertNodeHoldsDatasetBlocks(IArchivistNode node, ContentId cid, IndexSet expectedIndices, bool allowExtras = false)
        {
            var actual = node.GetDatasetStatus(cid);

            Log($"{node.GetName()} Expected: {expectedIndices}");
            Log($"{node.GetName()} Actual:   {actual.Blocks}");

            Assert.Multiple(() =>
            {
                Assert.That(actual.State, Is.EqualTo(DatasetStatusState.Completed));
                if (allowExtras)
                {
                    Assert.That(actual.Blocks.Includes(expectedIndices), $"{node.GetName()} is not holding the expected block indices. (extras allowed)");
                }
                else
                {
                    Assert.That(actual.Blocks, Is.EqualTo(expectedIndices), $"{node.GetName()} is not holding the expected block indices. (strict, no extras allowed)");
                }
            });
        }

        public void ShowBlocks(ContentId cid, params IArchivistNode[] nodes)
        {
            foreach (var n in nodes)
            {
                try
                {
                    var localFiles = n.LocalFiles();
                    if (localFiles.Content.Any(c => c.Cid == cid))
                    {
                        n.GetDatasetStatus(cid);
                        continue;
                    }
                }
                catch
                {
                }
                Log($"Dataset {cid} not present in node {n.GetName()}");
            }
        }

        public void ShowBlocks(ContentId cid, IArchivistNodeGroup nodes)
        {
            ShowBlocks(cid, nodes.ToArray());
        }

        private string GetBasicNodeStatus(IArchivistNode node)
        {
            return JsonConvert.SerializeObject(node.GetDebugInfo(), Formatting.Indented) + Environment.NewLine +
                node.Space().ToString() + Environment.NewLine;
        }

        protected virtual void OnArchivistSetup(IArchivistSetup setup)
        {
        }

        private CreateTranscriptAttribute? GetTranscriptAttributeOfCurrentTest()
        {
            var attrs = GetCurrentTestMethodAttribute<CreateTranscriptAttribute>();
            if (attrs.Any()) return attrs.Single();
            return null;
        }

        private ArchivistTranscriptWriter? SetupTranscript()
        {
            var attr = GetTranscriptAttributeOfCurrentTest();
            if (attr == null) return null;

            var config = new ArchivistTranscriptWriterConfig(
                attr.OutputFilename,
                attr.IncludeBlockReceivedEvents
            );

            var log = new LogPrefixer(GetTestLog(), "(Transcript) ");
            var writer = new ArchivistTranscriptWriter(log, config, Transcript.NewWriter(log));
            Ci.AddArchivistHooksProvider(writer);
            return writer;
        }

        private void TeardownTranscript()
        {
            if (writer == null) return;

            var result = GetTestResult();
            var log = GetTestLog();
            writer.AddResult(result.Success, result.Result);
            try
            {
                Stopwatch.Measure(log, "Transcript.ProcessLogs", () =>
                {
                    writer.ProcessLogs(DownloadAllLogs());
                });

                Stopwatch.Measure(log, $"Transcript.FinalizeWriter", () =>
                {
                    writer.IncludeFile(log.GetFullName() + ".log");
                    writer.FinalizeWriter();
                });
            }
            catch (Exception ex)
            {
                log.Error("Failure during transcript teardown: " + ex);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class CreateTranscriptAttribute : PropertyAttribute
    {
        public CreateTranscriptAttribute(string outputFilename, bool includeBlockReceivedEvents = true)
        {
            OutputFilename = outputFilename;
            IncludeBlockReceivedEvents = includeBlockReceivedEvents;
        }

        public string OutputFilename { get; }
        public bool IncludeBlockReceivedEvents { get; }
    }
}
