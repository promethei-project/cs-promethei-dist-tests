using PrometheiContractsPlugin;
using GethPlugin;

namespace PrometheiPlugin
{
    public class MarketplaceInitialConfig
    {
        public MarketplaceInitialConfig(MarketplaceSetup marketplaceSetup, IGethNode gethNode, IPrometheiContracts prometheiContracts)
        {
            MarketplaceSetup = marketplaceSetup;
            GethNode = gethNode;
            PrometheiContracts = prometheiContracts;

            // Currently (12-03-2026) devnet and testnet are running on an HTTPS connection to
            // an Arbitrum node which (I think) doesn't support websockets.
            // Should you live in a future where we at Promethei want to support websocket RPC
            // connections: This option is for you.
            UseWebsocketRPC = false;
        }

        public MarketplaceSetup MarketplaceSetup { get; }
        public IGethNode GethNode { get; }
        public IPrometheiContracts PrometheiContracts { get; }
        public bool UseWebsocketRPC { get; }
    }
}
