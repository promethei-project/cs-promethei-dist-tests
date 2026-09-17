using PrometheiClient;
using KubernetesWorkflow.Types;
using Utils;

namespace PrometheiPlugin
{
    public static class PrometheiInstanceContainerExtension
    {
        public static IPrometheiInstance CreateFromPod(RunningPod pod)
        {
            var container = pod.Containers.Single();

            return new PrometheiInstance(
                name: container.Name,
                imageName: container.Recipe.Image,
                startUtc: container.Recipe.RecipeCreatedUtc,
                discoveryEndpoint: SetClusterInternalIpAddress(pod, container.GetInternalAddress(PrometheiContainerRecipe.DiscoveryPortTag)),
                apiEndpoint: container.GetAddress(PrometheiContainerRecipe.ApiPortTag),
                listenEndpoint: container.GetInternalAddress(PrometheiContainerRecipe.ListenPortTag),
                ethAccount: container.Recipe.Additionals.Get<EthAccount>(),
                metricsEndpoint: GetMetricsEndpoint(container)
            );
        }

        private static Address SetClusterInternalIpAddress(RunningPod pod, Address address)
        {
            return new Address(
                logName: address.LogName,
                host: pod.PodInfo.Ip,
                port: address.Port
            );
        }

        private static Address? GetMetricsEndpoint(RunningContainer container)
        {
            try
            {
                return container.GetInternalAddress(PrometheiContainerRecipe.MetricsPortTag);
            }
            catch
            {
                return null;
            }
        }
    }
}
