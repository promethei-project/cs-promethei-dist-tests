using PrometheiClient;
using PrometheiClient.Hooks;
using Core;

namespace PrometheiPlugin
{
    public static class CoreInterfaceExtensions
    {
        public static IPrometheiInstance[] DeployPrometheiNodes(this CoreInterface ci, int number, Action<IPrometheiSetup> setup)
        {
            return Plugin(ci).DeployPrometheiNodes(number, setup);
        }

        public static IPrometheiNodeGroup WrapPrometheiContainers(this CoreInterface ci, IPrometheiInstance[] instances)
        {
            return Plugin(ci).WrapPrometheiContainers(instances);
        }

        public static IPrometheiNode StartPrometheiNode(this CoreInterface ci)
        {
            return ci.StartPrometheiNodes(1)[0];
        }

        public static IPrometheiNode StartPrometheiNode(this CoreInterface ci, Action<IPrometheiSetup> setup)
        {
            return ci.StartPrometheiNodes(1, setup)[0];
        }

        public static IPrometheiNodeGroup StartPrometheiNodes(this CoreInterface ci, int number, Action<IPrometheiSetup> setup)
        {
            var rc = ci.DeployPrometheiNodes(number, setup);
            var result = ci.WrapPrometheiContainers(rc);
            Plugin(ci).WireUpMarketplace(result, setup);
            return result;
        }

        public static IPrometheiNodeGroup StartPrometheiNodes(this CoreInterface ci, int number)
        {
            return ci.StartPrometheiNodes(number, s => { });
        }

        public static void AddPrometheiHooksProvider(this CoreInterface ci, IPrometheiHooksProvider hooksProvider)
        {
            Plugin(ci).AddPrometheiHooksProvider(hooksProvider);
        }

        private static PrometheiPlugin Plugin(CoreInterface ci)
        {
            return ci.GetPlugin<PrometheiPlugin>();
        }
    }
}
