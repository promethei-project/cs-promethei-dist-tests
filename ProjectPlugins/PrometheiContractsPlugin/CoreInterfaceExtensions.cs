using Core;

namespace PrometheiContractsPlugin
{
    public static class CoreInterfaceExtensions
    {
        public static PrometheiContractsDeployment DeployPrometheiContracts(this CoreInterface ci, Action<IPrometheiContractsSetup> setup)
        {
            return Plugin(ci).DeployContracts(ci, setup);
        }

        public static IPrometheiContracts WrapPrometheiContractsDeployment(this CoreInterface ci, PrometheiContractsDeployment deployment, Action<IPrometheiContractsSetup> setup)
        {
            return Plugin(ci).WrapDeploy(deployment, setup);
        }

        public static IPrometheiContracts StartPrometheiContracts(this CoreInterface ci, Action<IPrometheiContractsSetup> setup)
        {
            var deployment = DeployPrometheiContracts(ci, setup);
            return WrapPrometheiContractsDeployment(ci, deployment, setup);
        }

        private static PrometheiContractsPlugin Plugin(CoreInterface ci)
        {
            return ci.GetPlugin<PrometheiContractsPlugin>();
        }
    }
}
