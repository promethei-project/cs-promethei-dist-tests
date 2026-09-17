using PrometheiClient;
using PrometheiContractsPlugin;
using PrometheiContractsPlugin.ChainMonitor;
using PrometheiNetworkConfig;
using ArgsUniform;
using BlockchainUtils;
using ChainFollowingApp;
using ContractDataChecker;
using Logging;

public class Program
{
    public static Task Main(string[] args)
    {
        var uniformArgs = new ArgsUniform<Configuration>(args);
        var config = uniformArgs.Parse(true);

        var p = new Program(config);
        return p.Run();
    }

    private readonly ChainFollowing chainFollower;
    private readonly CancellationToken ct;

    public Program(Configuration config)
    {
        var cts = new CancellationTokenSource();
        ct = cts.Token;
        Console.CancelKeyPress += (sender, args) => cts.Cancel();

        var log = new LogPrefixer(new TimestampPrefixer(new LogSplitter(
             new ConsoleLog(),
             new FileLog(Path.Combine(config.LogPath, "cdt"))
         )), "(CDT)");

        log.Log("  --  [[ Contract Data Tester ]]  --");
        log.Log("Getting Promethei network configuration...");
        var netConnector = new PrometheiNetworkConnector(log);
        var network = netConnector.GetConfig();

        AddKnownHostsLogReplacements(log, network);

        log.Log("Initializing RPC connector...");
        var diskStore = new DiskBlockBucketStore(log, Path.Join(config.DataPath, "blockcache"));
        var blockCache = new BlockCache(log, diskStore);
        var requestsCache = new DiskRequestsCache(Path.Join(config.DataPath, "requestscache"));
        var rpcConnector = GethConnector.GethConnector.Initialize(log, network, blockCache, requestsCache);
        if (rpcConnector == null) throw new Exception("Invalid Eth RPC information");

        log.Log("Creating Promethei client instance...");
        var factory = new PrometheiNodeFactory(log, "datadir");
        var endpoint = config.PrometheiEndpoint;
        var splitIndex = endpoint.LastIndexOf(':');
        var host = endpoint.Substring(0, splitIndex);
        var port = Convert.ToInt32(endpoint.Substring(splitIndex + 1));
        var instance = PrometheiInstance.CreateFromApiEndpoint(
            "node",
            new Utils.Address("node", host, port)
        );
        var prometheiNode = factory.CreatePrometheiNode(instance);

        chainFollower = new ChainFollowing(new ChainFollowConfig(
            log,
            config.Interval,
            config.HistoryStartUtc,
            rpcConnector.GethNode,
            rpcConnector.PrometheiContracts,
            requestsCache
        ), new ChainFollowHandlers(
            new DataChecker(log, config, prometheiNode),
            new DoNothingChainEventHandler(),
            null
        ));
     
        log.Log("Activating chain-follower...");
    }

    private void AddKnownHostsLogReplacements(ILog log, PrometheiNetwork network)
    {
        foreach (var nodeGroup in network.Team.Nodes)
        {
            foreach (var instance in nodeGroup.Instances)
            {
                AddKnownHostLogReplacement(instance, log);
            }
        }
    }

    private void AddKnownHostLogReplacement(PrometheiNetworkTeamNodesVersionsInstancesEntry instance, ILog log)
    {
        if (string.IsNullOrEmpty(instance.EthAddress)) return;
        if (string.IsNullOrEmpty(instance.Name)) return;

        log.AddStringReplace(instance.EthAddress, instance.Name);
    }

    private async Task Run()
    {
        await chainFollower.Run(ct);
    }
}
