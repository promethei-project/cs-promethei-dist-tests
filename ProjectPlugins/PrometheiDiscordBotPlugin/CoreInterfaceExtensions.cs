using Core;
using KubernetesWorkflow.Types;

namespace PrometheiDiscordBotPlugin
{
    public static class CoreInterfaceExtensions
    {
        public static RunningPod DeployPrometheiDiscordBot(this CoreInterface ci, DiscordBotStartupConfig config)
        {
            return Plugin(ci).Deploy(config);
        }

        public static RunningPod DeployRewarderBot(this CoreInterface ci, RewarderBotStartupConfig config)
        {
            return Plugin(ci).DeployRewarder(config);
        }

        private static PrometheiDiscordBotPlugin Plugin(CoreInterface ci)
        {
            return ci.GetPlugin<PrometheiDiscordBotPlugin>();
        }
    }
}
