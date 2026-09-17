using BlockchainUtils;
using PrometheiContractsPlugin;
using PrometheiContractsPlugin.Marketplace;
using Core;
using GethPlugin;
using Logging;
using Utils;
using PrometheiNetworkConfig;

namespace TraceContract
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ProjectPlugin.Load<GethPlugin.GethPlugin>();
            ProjectPlugin.Load<PrometheiContractsPlugin.PrometheiContractsPlugin>();

            var p = new Program();
            p.Run();
        }

        private readonly PrometheiNetwork network;
        private readonly ILog baseLog;
        private readonly ILog appLog;
        private readonly Input input = new();
        private readonly Config config = new();
        private readonly Output output;

        public Program()
        {
            baseLog = new TimestampPrefixer(
                new LogSplitter(
                    new ConsoleLog(),
                    new FileLog(Path.Combine(config.DataDir, "logs"))
                )
            );

            appLog = new LogPrefixer(baseLog, "(TraceContract)");

            var connector = new PrometheiNetworkConnector(baseLog);
            network = connector.GetConfig();

            output = new(appLog, input, config, network);
        }

        private void Run()
        {
            try
            {
                TracePurchase();
            }
            catch (Exception exc)
            {
                appLog.Error(exc.ToString());
            }
        }

        private void TracePurchase()
        { 
            Log("Setting up...");
            var entryPoint = new EntryPoint(baseLog, new KubernetesWorkflow.Configuration(null, TimeSpan.FromMinutes(1.0), TimeSpan.FromSeconds(10.0), "_Unused!_"), Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            entryPoint.Announce();
            var ci = entryPoint.CreateInterface();
            var geth = ConnectGethNode();
            var contracts = ConnectPrometheiContracts(ci, geth);

            output.LogRequestId(input.RequestId);
            var chainTracer = new ChainTracer(appLog, baseLog, geth, contracts, input, output);
            var requestTimeRange = chainTracer.TraceChainTimeline();

            Log("Downloading storage nodes logs for the request timerange...");
            DownloadStorageNodeLogs(requestTimeRange, entryPoint.Tools);

            output.ShowOutputFiles(appLog);

            entryPoint.Decommission(false, false, false);
            Log("Done");
        }

        private IGethNode ConnectGethNode()
        {
            var account = EthAccountGenerator.GenerateNew();
            var blockCache = new BlockCache(baseLog, new DiskBlockBucketStore(baseLog, Path.Combine(config.DataDir, "blocks_cache")));
            return new CustomGethNode(baseLog, blockCache, network.Team.Utils.BotRpc, account.PrivateKey);
        }

        private IPrometheiContracts ConnectPrometheiContracts(CoreInterface ci, IGethNode geth)
        {
            var deployment = new PrometheiContractsDeployment(
                config: new MarketplaceConfig(),
                marketplaceAddress: new ContractAddress(network.Marketplace.ContractAddress),
                abi: network.Marketplace.ABI
            );
            return ci.WrapPrometheiContractsDeployment(deployment, s => s
                .WithRpcNode(geth)
                .WithRequestsCache(               
                    new DiskRequestsCache(Path.Combine(config.DataDir, "requests_cache"))
                )
            );
        }

        private void DownloadStorageNodeLogs(TimeRange requestTimeRange, IPluginTools tools)
        {
            var start = requestTimeRange.From - config.LogStartBeforeStorageContractStarts;

            var podNames = GetStorageNodesPodNames();
            foreach (var podName in podNames)
            {
                Log($"Downloading logs from '{podName}'...");

                var targetFile = output.CreateNodeLogTargetFile(podName);
                var downloader = new ElasticSearchLogDownloader(baseLog, tools, config, network);
                downloader.Download(targetFile, podName, start, requestTimeRange.To);
            }
        }

        private string[] GetStorageNodesPodNames()
        {
            return network.Team
                .Nodes.Single(c => c.Category.ToLowerInvariant() == "hosts")
                .Instances.Select(i => i.PodName).ToArray();
        }

        private void Log(string msg)
        {
            appLog.Log(msg);
        }
    }
}
