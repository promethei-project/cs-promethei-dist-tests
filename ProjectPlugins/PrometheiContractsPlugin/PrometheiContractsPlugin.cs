using Core;

namespace PrometheiContractsPlugin
{
    public class PrometheiContractsPlugin : IProjectPlugin, IHasLogPrefix, IHasMetadata
    {
        private readonly IPluginTools tools;
        private readonly PrometheiContractsStarter starter;

        public PrometheiContractsPlugin(IPluginTools tools)
        {
            this.tools = tools;
            starter = new PrometheiContractsStarter(tools);
        }

        public string LogPrefix => "(PrometheiContracts) ";

        public void Awake(IPluginAccess access)
        {
        }

        public void Announce()
        {
            tools.GetLog().Log($"Loaded Promethei-Marketplace SmartContracts");
        }

        public void AddMetadata(IAddMetadata metadata)
        {
            metadata.Add("prometheicontractsid", "dynamic");
        }

        public void Decommission()
        {
        }

        public PrometheiContractsDeployment DeployContracts(CoreInterface ci, Action<IPrometheiContractsSetup> setup)
        {
            return starter.Deploy(ci, setup);
        }

        public IPrometheiContracts WrapDeploy(PrometheiContractsDeployment deployment, Action<IPrometheiContractsSetup> setup)
        {
            deployment = SerializeGate.Gate(deployment);
            return starter.Wrap(deployment, setup);
        }
    }
}
