using PrometheiContractsPlugin.Marketplace;
using Utils;

namespace PrometheiContractsPlugin
{
    public class PrometheiContractsDeployment
    {
        public PrometheiContractsDeployment(MarketplaceConfig config, ContractAddress marketplaceAddress, string abi)
        {
            Config = config;
            MarketplaceAddress = marketplaceAddress;
            Abi = abi;
        }

        public MarketplaceConfig Config { get; }
        public ContractAddress MarketplaceAddress { get; }
        public string Abi { get; }
    }
}
