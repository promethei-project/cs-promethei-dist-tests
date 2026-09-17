using PrometheiContractsPlugin.ChainMonitor;
using ChainFollowingApp;
using DiscordRewards;
using Logging;

namespace TestNetRewarder
{
    public class ChainFollowHooksHandler : IChainFollowingHooks
    {
        private readonly ILog log;
        private readonly Configuration config;
        private readonly EventsFormatter eventsFormatter;
        private readonly RequestBuilder builder;
        private readonly IBotClient client;
        private readonly CancellationToken ct;
        private ChainState currentChainState = null!;

        public ChainFollowHooksHandler(ILog log, Configuration config, EventsFormatter eventsFormatter, RequestBuilder builder, IBotClient client, CancellationToken ct)
        {
            this.log = log;
            this.config = config;
            this.eventsFormatter = eventsFormatter;
            this.builder = builder;
            this.client = client;
            this.ct = ct;
        }

        public void OnError(string msg)
        {
            eventsFormatter.OnError(msg);
        }

        public async Task OnInitialized(ChainState chainState, int recoveredRequests)
        {
            currentChainState = chainState;

            var events = eventsFormatter.GetInitializationEvents(config, recoveredRequests);
            log.Log("Building initial state...");
            var request = builder.Build(chainState, events, Array.Empty<string>());
            if (request.HasAny())
            {
                await client.SendRewards(request);
            }
        }

        public async Task OnRunStarting()
        {
            await client.EnsureBotOnline(ct);
        }

        public async Task OnLoopStepStarting()
        {
            await client.EnsureBotOnline(ct);
        }

        public async Task OnLoopStepFinished(bool isRealtime)
        {
            var events = eventsFormatter.GetEvents();
            var errors = eventsFormatter.GetErrors();

            var request = builder.Build(currentChainState, events, errors);
            if (request.HasAny())
            {
                await client.SendRewards(request);
            }
        }
    }
}
