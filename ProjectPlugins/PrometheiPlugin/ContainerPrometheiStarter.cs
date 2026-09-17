using PrometheiClient;
using Core;
using KubernetesWorkflow;
using KubernetesWorkflow.Types;
using Utils;

namespace PrometheiPlugin
{
    public class ContainerPrometheiStarter : IPrometheiStarter
    {
        private readonly IPluginTools pluginTools;
        private readonly ProcessControlMap processControlMap;
        private readonly PrometheiContainerRecipe recipe;
        private readonly ApiChecker apiChecker;

        public ContainerPrometheiStarter(IPluginTools pluginTools, PrometheiContainerRecipe recipe, ProcessControlMap processControlMap)
        {
            this.pluginTools = pluginTools;
            this.recipe = recipe;
            this.processControlMap = processControlMap;
            apiChecker = new ApiChecker(pluginTools);
        }

        public IPrometheiInstance[] BringOnline(PrometheiSetup prometheiSetup)
        {
            LogSeparator();
            Log($"Starting {prometheiSetup.Describe()}...");

            var startupConfig = CreateStartupConfig(prometheiSetup);

            var containers = StartPrometheiContainers(startupConfig, prometheiSetup.NumberOfNodes, prometheiSetup.Location);

            apiChecker.CheckCompatibility(containers);

            foreach (var rc in containers)
            {
                var podInfo = GetPodInfo(rc);
                var podInfos = string.Join(", ", rc.Containers.Select(c => $"Container: '{c.Name}' PodLabel: '{c.RunningPod.StartResult.Deployment.PodLabel}' runs at '{podInfo.K8SNodeName}'={podInfo.Ip}"));
                Log($"Started node with image '{containers.First().Containers.First().Recipe.Image}'. ({podInfos})");
                LogEthAddress(rc);
            }
            LogSeparator();

            return containers.Select(CreateInstance).ToArray();
        }

        public void Decommission()
        {
        }

        private StartupConfig CreateStartupConfig(PrometheiSetup prometheiSetup)
        {
            var startupConfig = new StartupConfig();
            startupConfig.NameOverride = prometheiSetup.NameOverride;
            startupConfig.Add(prometheiSetup);
            return startupConfig;
        }

        private RunningPod[] StartPrometheiContainers(StartupConfig startupConfig, int numberOfNodes, ILocation location)
        {
            var futureContainers = new List<FutureContainers>();
            for (var i = 0; i < numberOfNodes; i++)
            {
                var workflow = pluginTools.CreateWorkflow();
                futureContainers.Add(workflow.Start(1, location, recipe, startupConfig));
            }

            return futureContainers
                .Select(f => f.WaitForOnline())
                .ToArray();
        }

        private PodInfo GetPodInfo(RunningPod rc)
        {
            var workflow = pluginTools.CreateWorkflow();
            return workflow.GetPodInfo(rc);
        }

        private IPrometheiInstance CreateInstance(RunningPod pod)
        {
            var instance = PrometheiInstanceContainerExtension.CreateFromPod(pod);
            var processControl = new PrometheiContainerProcessControl(pluginTools, pod, onStop: () =>
            {
                processControlMap.Remove(instance);
            });
            processControlMap.Add(instance, processControl);
            return instance;
        }

        private void LogSeparator()
        {
            Log("----------------------------------------------------------------------------");
        }

        private void LogEthAddress(RunningPod rc)
        {
            var account = rc.Containers.First().Recipe.Additionals.Get<EthAccount>();
            if (account == null) return;
            Log($"{rc.Name} = {account}");
        }

        private void Log(string message)
        {
            pluginTools.GetLog().Log(message);
        }
    }
}
