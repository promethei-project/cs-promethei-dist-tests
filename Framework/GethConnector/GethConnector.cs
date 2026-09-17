using BlockchainUtils;
using PrometheiContractsPlugin;
using PrometheiContractsPlugin.Marketplace;
using GethPlugin;
using Logging;
using PrometheiNetworkConfig;
using Utils;

namespace GethConnector
{
    public class GethConnector
    {
        public IGethNode GethNode { get; }
        public IPrometheiContracts PrometheiContracts { get; }

        private const string GethPrivKeyVar = "GETH_PRIVATE_KEY";

        public static GethConnector? Initialize(ILog log)
        {
            return Initialize(log, FetchNetworkConfig(log), new BlockCache(log), new NullRequestsCache());
        }

        public static GethConnector? Initialize(ILog log, PrometheiNetwork network)
        {
            return Initialize(log, network, new BlockCache(log), new NullRequestsCache());
        }

        public static GethConnector? Initialize(ILog log, BlockCache blockCache, IRequestsCache requestsCache)
        {
            return Initialize(log, FetchNetworkConfig(log), blockCache, requestsCache);
        }

        public static GethConnector? Initialize(ILog log, PrometheiNetwork networkConfig, BlockCache blockCache, IRequestsCache requestsCache)
        {
            var privateKey = EnvVar.GetOrThrow(GethPrivKeyVar);

            var gethNode = new CustomGethNode(log, blockCache, networkConfig.Team.Utils.BotRpc, privateKey);
            var config = GetPrometheiMarketplaceConfig(gethNode, new ContractAddress(networkConfig.Marketplace.ContractAddress));

            var contractsDeployment = new PrometheiContractsDeployment(
                config: config,
                marketplaceAddress: new ContractAddress(networkConfig.Marketplace.ContractAddress),
                abi: networkConfig.Marketplace.ABI
            );

            var contracts = new PrometheiContractsAccess(log, gethNode, contractsDeployment, requestsCache);

            return new GethConnector(gethNode, contracts);
        }

        private static PrometheiNetwork FetchNetworkConfig(ILog log)
        {
            try
            {
                var networkConnector = new PrometheiNetworkConnector(log);
                return networkConnector.GetConfig();
            }
            catch (Exception ex)
            {
                log.Error($"Unable to load PrometheiNetworkConfig: " + ex);
                throw;
            }
        }

        private static MarketplaceConfig GetPrometheiMarketplaceConfig(IGethNode gethNode, ContractAddress marketplaceAddress)
        {
            var func = new ConfigurationFunctionBase();
            var response = gethNode.Call<ConfigurationFunctionBase, ConfigurationOutputDTO>(marketplaceAddress, func);
            return response.ReturnValue1;
        }

        private GethConnector(IGethNode gethNode, IPrometheiContracts prometheiContracts)
        {
            GethNode = gethNode;
            PrometheiContracts = prometheiContracts;
        }
    }
}
