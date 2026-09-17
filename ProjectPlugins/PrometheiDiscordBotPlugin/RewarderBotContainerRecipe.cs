using KubernetesWorkflow.Recipe;
using KubernetesWorkflow;
using Utils;

namespace PrometheiDiscordBotPlugin
{
    public class RewarderBotContainerRecipe : ContainerRecipeFactory
    {
        private const string DockerImageEnvVar = "PROMETHEI_REWARDERBOT_IMAGE";
        private const string ImagePullPolicyEnvVar = "PROMETHEI_REWARDERBOT_IMAGE_PULL_POLICY";
        private const string DefaultDockerImage = "durabilitylabs/promethei-rewarderbot:sha-f5ae024";

        public override string AppName => "discordbot-rewarder";
        public override string Image => EnvVar.GetOrDefault(DockerImageEnvVar, DefaultDockerImage);
        public override string? ImagePullPolicy => EnvVar.GetNullableOrDefault(ImagePullPolicyEnvVar);

        protected override void Initialize(StartupConfig startupConfig)
        {
            var config = startupConfig.Get<RewarderBotStartupConfig>();

            SetSchedulingAffinity(notIn: "false");

            AddEnvVar("DISCORDBOTHOST", config.DiscordBotHost);
            AddEnvVar("DISCORDBOTPORT", config.DiscordBotPort.ToString());
            AddEnvVar("INTERVALMINUTES", config.IntervalMinutes.ToString());
            AddEnvVar("CHECKHISTORY", Time.ToUnixTimeSeconds(config.HistoryStartUtc).ToString());

            var gethInfo = config.GethInfo;
            AddEnvVar("GETH_HOST", gethInfo.Host);
            AddEnvVar("GETH_HTTP_PORT", gethInfo.Port.ToString());
            AddEnvVar("GETH_PRIVATE_KEY", gethInfo.PrivKey);
            AddEnvVar("PROMETHEICONTRACTS_MARKETPLACEADDRESS", gethInfo.MarketplaceAddress);
            AddEnvVar("PROMETHEICONTRACTS_ABI", gethInfo.Abi);

            if (!string.IsNullOrEmpty(config.DataPath))
            {
                AddEnvVar("DATAPATH", config.DataPath);
                AddVolume(config.DataPath, 1.GB());
            }
        }
    }
}
