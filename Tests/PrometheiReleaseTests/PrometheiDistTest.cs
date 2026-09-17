using BlockchainUtils;
using PrometheiClient;
using PrometheiContractsPlugin;
using PrometheiPlugin;
using PrometheiPlugin.OverwatchSupport;
using PrometheiTests.Helpers;
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
using PrometheiOpenApi;

namespace PrometheiTests
{
    public class PrometheiDistTest : DistTest
    {
        private readonly BlockCache blockCache = new BlockCache(new NullLog());
        private readonly List<IPrometheiNode> nodes = new List<IPrometheiNode>();
        private PrometheiTranscriptWriter? writer;

        public PrometheiDistTest()
        {
            ProjectPlugin.Load<PrometheiPlugin.PrometheiPlugin>();
            ProjectPlugin.Load<PrometheiContractsPlugin.PrometheiContractsPlugin>();
            ProjectPlugin.Load<GethPlugin.GethPlugin>();
            ProjectPlugin.Load<MetricsPlugin.MetricsPlugin>();
        }

        [SetUp]
        public void SetupPrometheiDistTest()
        {
            writer = SetupTranscript();
        }

        [TearDown]
        public void TearDownPrometheiDistTest()
        {
            TeardownTranscript();
        }

        protected override void Initialize(FixtureLog fixtureLog)
        {
            Ci.AddPrometheiHooksProvider(new PrometheiLogTrackerProvider(nodes.Add));
        }

        public IPrometheiNode StartPromethei()
        {
            return StartPromethei(s => { });
        }

        public IPrometheiNode StartPromethei(Action<IPrometheiSetup> setup)
        {
            return StartPromethei(1, setup)[0];
        }

        public IPrometheiNodeGroup StartPromethei(int numberOfNodes)
        {
            return StartPromethei(numberOfNodes, s => { });
        }

        public IPrometheiNodeGroup StartPromethei(int numberOfNodes, Action<IPrometheiSetup> setup)
        {
            var group = Ci.StartPrometheiNodes(numberOfNodes, s =>
            {
                setup(s);
                OnPrometheiSetup(s);
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

        public void AssertBalance(IPrometheiContracts contracts, IPrometheiNode prometheiNode, Constraint constraint, string msg)
        {
            Assert.Fail("Depricated, use MarketplaceAutobootstrapDistTest assertBalances instead.");
            AssertHelpers.RetryAssert(constraint, () => contracts.GetTestTokenBalance(prometheiNode), nameof(AssertBalance) + msg);
        }

        public void CheckLogForErrors(params IPrometheiNode[] nodes)
        {
            foreach (var node in nodes) CheckLogForErrors(node);
        }

        public void CheckLogForErrors(IPrometheiNode node)
        {
            Log($"Checking {node.GetName()} log for errors.");
            var log = node.DownloadLog();

            log.AssertLogDoesNotContain("Block validation failed");
            log.AssertLogDoesNotContainLinesStartingWith("ERR ");
        }

        public void LogNodeStatus(IPrometheiNode node, IMetricsAccess? metrics = null)
        {
            Log("Status for " + node.GetName() + Environment.NewLine +
                GetBasicNodeStatus(node));
        }

        public void WaitAndCheckNodesStaysAlive(TimeSpan duration, IPrometheiNodeGroup nodes)
        {
            WaitAndCheckNodesStaysAlive(duration, nodes.ToArray());
        }

        public void WaitAndCheckNodesStaysAlive(TimeSpan duration, List<IPrometheiNode> nodes)
        {
            WaitAndCheckNodesStaysAlive(duration, nodes.ToArray());
        }

        public void WaitAndCheckNodesStaysAlive(TimeSpan duration, params IPrometheiNode[] nodes)
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

        public void AssertNodesContainFile(ContentId cid, IPrometheiNodeGroup nodes)
        {
            AssertNodesContainFile(cid, nodes.ToArray());
        }

        public void AssertNodesContainFile(ContentId cid, params IPrometheiNode[] nodes)
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

        public void AssertNodeHoldsDatasetBlocks(IPrometheiNode node, ContentId cid, IndexSet expectedIndices, bool allowExtras = false)
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

        public void ShowBlocks(ContentId cid, params IPrometheiNode[] nodes)
        {
            foreach (var n in nodes)
            {
                var localFiles = n.LocalFiles();
                if (localFiles.Content.Any(c => c.Cid == cid))
                {
                    try
                    {
                        n.GetDatasetStatus(cid);
                        continue;
                    }
                    catch (ApiException e)
                    {
                        if (e.Message != "Dataset specified by the CID is not found") throw;
                    }
                }
                Log($"Dataset {cid} not present in node {n.GetName()}");
            }
        }

        public void ShowBlocks(ContentId cid, IPrometheiNodeGroup nodes)
        {
            ShowBlocks(cid, nodes.ToArray());
        }

        private string GetBasicNodeStatus(IPrometheiNode node)
        {
            return JsonConvert.SerializeObject(node.GetDebugInfo(), Formatting.Indented) + Environment.NewLine +
                node.Space().ToString() + Environment.NewLine;
        }

        protected virtual void OnPrometheiSetup(IPrometheiSetup setup)
        {
        }

        private CreateTranscriptAttribute? GetTranscriptAttributeOfCurrentTest()
        {
            var attrs = GetCurrentTestMethodAttribute<CreateTranscriptAttribute>();
            if (attrs.Any()) return attrs.Single();
            return null;
        }

        private PrometheiTranscriptWriter? SetupTranscript()
        {
            var attr = GetTranscriptAttributeOfCurrentTest();
            if (attr == null) return null;

            var config = new PrometheiTranscriptWriterConfig(
                attr.OutputFilename,
                attr.IncludeBlockReceivedEvents
            );

            var log = new LogPrefixer(GetTestLog(), "(Transcript) ");
            var writer = new PrometheiTranscriptWriter(log, config, Transcript.NewWriter(log));
            Ci.AddPrometheiHooksProvider(writer);
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
