using PrometheiClient;
using PrometheiClient.Hooks;
using Core;

namespace PrometheiPlugin
{
    public class PrometheiPlugin : IProjectPlugin, IHasLogPrefix, IHasMetadata
    {
        private const bool UseContainers = true;

        private readonly IPrometheiStarter prometheiStarter;
        private readonly IPluginTools tools;
        private readonly PrometheiLogLevel defaultLogLevel = PrometheiLogLevel.Trace;
        private readonly PrometheiHooksFactory hooksFactory = new PrometheiHooksFactory();
        private readonly ProcessControlMap processControlMap = new ProcessControlMap();
        private readonly PrometheiDockerImage prometheiDockerImage = new PrometheiDockerImage();
        private readonly PrometheiContainerRecipe recipe;
        private readonly PrometheiWrapper prometheiWrapper;

        public PrometheiPlugin(IPluginTools tools)
        {
            this.tools = tools;

            recipe = new PrometheiContainerRecipe();
            prometheiStarter = CreatePrometheiStarter();
            prometheiWrapper = new PrometheiWrapper(tools, processControlMap, hooksFactory);
        }

        private IPrometheiStarter CreatePrometheiStarter()
        {
            if (UseContainers)
            {
                Log("Using Containerized Promethei instances");
                return new ContainerPrometheiStarter(tools, recipe, processControlMap);
            }

            Log("Using Binary Promethei instances");
            return new BinaryPrometheiStarter(tools, processControlMap);
        }

        public string LogPrefix => "(Promethei) ";

        public void Awake(IPluginAccess access)
        {
        }

        public void Announce()
        {
            // give promethei docker image to contracts plugin.

            Log($"Loaded with Promethei ID: '{prometheiWrapper.GetPrometheiId()}' - Revision: {prometheiWrapper.GetPrometheiRevision()}");
        }

        public void AddMetadata(IAddMetadata metadata)
        {
            metadata.Add("prometheiid", prometheiWrapper.GetPrometheiId());
            metadata.Add("prometheirevision", prometheiWrapper.GetPrometheiRevision());
        }

        public void Decommission()
        {
            prometheiStarter.Decommission();
        }

        public IPrometheiInstance[] DeployPrometheiNodes(int numberOfNodes, Action<IPrometheiSetup> setup)
        {
            var prometheiSetup = GetSetup(numberOfNodes, setup);
            return prometheiStarter.BringOnline(prometheiSetup);
        }

        public IPrometheiNodeGroup WrapPrometheiContainers(IPrometheiInstance[] instances)
        {
            instances = instances.Select(c => SerializeGate.Gate(c as PrometheiInstance)!).ToArray();
            return prometheiWrapper.WrapPrometheiInstances(instances);
        }

        public void WireUpMarketplace(IPrometheiNodeGroup result, Action<IPrometheiSetup> setup)
        {
            var prometheiSetup = GetSetup(1, setup);
            if (prometheiSetup.MarketplaceConfig == null) return;
            
            var mconfig = prometheiSetup.MarketplaceConfig;
            foreach (var node in result)
            {
                mconfig.GethNode.SendEth(node, mconfig.MarketplaceSetup.InitialEth);
                mconfig.PrometheiContracts.MintTestTokens(node, mconfig.MarketplaceSetup.InitialTestTokens);

                Log($"Sent {mconfig.MarketplaceSetup.InitialEth} and " +
                    $"minted {mconfig.MarketplaceSetup.InitialTestTokens} for " +
                    $"{node.GetName()} (address: {node.EthAddress})");
            }
        }

        public void AddPrometheiHooksProvider(IPrometheiHooksProvider hooksProvider)
        {
            if (hooksFactory.Providers.Contains(hooksProvider)) return;
            hooksFactory.Providers.Add(hooksProvider);
        }

        private PrometheiSetup GetSetup(int numberOfNodes, Action<IPrometheiSetup> setup)
        {
            var prometheiSetup = new PrometheiSetup(prometheiDockerImage, numberOfNodes);
            prometheiSetup.LogLevel = defaultLogLevel;
            setup(prometheiSetup);
            return prometheiSetup;
        }

        private void Log(string msg)
        {
            tools.GetLog().Log(msg);
        }
    }
}
