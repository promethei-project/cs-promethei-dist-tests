using PrometheiContractsPlugin;
using PrometheiNetworkConfig;
using ArgsUniform;
using BlockchainUtils;
using ChainFollowingApp;
using DiscordRewards;
using Logging;

namespace TestNetRewarder
{
    public class Program
    {
        public static Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            var ct = cts.Token;
            Console.CancelKeyPress += (sender, args) => cts.Cancel();

            var uniformArgs = new ArgsUniform<Configuration>(args);
            var config = uniformArgs.Parse(true);

            var log = new TimestampPrefixer(                
                new LogSplitter(
                    new FileLog(Path.Combine(config.LogPath, "testnetrewarder")),
                    new ConsoleLog()
                )
            );

            var networkConnector = new PrometheiNetworkConnector(log);
            var network = networkConnector.GetConfig();

            var diskStore = new DiskBlockBucketStore(log, Path.Join(config.DataPath, "blockcache"));
            var blockCache = new BlockCache(log, diskStore);
            var requestsCache = new DiskRequestsCache(Path.Join(config.DataPath, "requestscache"));
            var connector = GethConnector.GethConnector.Initialize(log, network, blockCache, requestsCache);
            if (connector == null) throw new Exception("Invalid Eth RPC information");

            var builder = new RequestBuilder();
            var lookup = new ContentInformationLookup(config, network);
            var botClient = new BotClient(config.DiscordHost, config.DiscordPort, log);

            var eventsFormatter = new EventsFormatter(lookup, connector.PrometheiContracts.Deployment.Config);
            var periodMonitorHandler = new PeriodMonitorHandler(eventsFormatter);

            var hooks = new ChainFollowHooksHandler(log, config, eventsFormatter, builder, botClient, ct);

            var followConfig = new ChainFollowConfig(log, config.Interval, config.HistoryStartUtc, connector.GethNode, connector.PrometheiContracts, requestsCache);
            var handlers = new ChainFollowHandlers(hooks, eventsFormatter, config.ShowProofsMissed > 0 ? periodMonitorHandler : null);

            var chainFollower = new ChainFollowing(followConfig, handlers);

            EnsurePath(config.DataPath);
            EnsurePath(config.LogPath);

            return new Program().MainAsync(log, chainFollower, botClient, ct);
        }

        public async Task MainAsync(ILog log, ChainFollowing chain, IBotClient botClient, CancellationToken ct)
        {
            log.Log("Starting Testnet Rewarder...");
            await botClient.EnsureBotOnline(ct);

            await chain.Run(ct);
        }

        private static void EnsurePath(string path)
        {
            if (Directory.Exists(path)) return;
            Directory.CreateDirectory(path);
        }
    }
}
