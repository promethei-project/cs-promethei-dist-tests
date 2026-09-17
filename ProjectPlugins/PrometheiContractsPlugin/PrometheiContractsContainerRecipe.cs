using PrometheiClient;
using GethPlugin;
using KubernetesWorkflow;
using KubernetesWorkflow.Recipe;
using Utils;

namespace PrometheiContractsPlugin
{
    public class PrometheiContractsContainerRecipe : ContainerRecipeFactory
    {
        public const string DeployedAddressesFilename = "/hardhat/ignition/deployments/chain-789988/deployed_addresses.json";
        public const string MarketplaceArtifactFilename = "/hardhat/artifacts/contracts/Marketplace.sol/Marketplace.json";

        public const int PeriodSeconds = 60;
        public const int TimeoutSeconds = 30;
        public const int DowntimeSeconds = 128;
        private const string DockerImageEnvVar = "PROMETHEI_CONTRACTS_IMAGE";
        private const string ImagePullPolicyEnvVar = "PROMETHEI_CONTRACTS_IMAGE_PULL_POLICY";
        private readonly DebugInfoVersion versionInfo;

        public override string AppName => "promethei-contracts";
        public override string Image => EnvVar.GetOrDefault(DockerImageEnvVar, GetDefaultContractsDockerImage());
        public override string? ImagePullPolicy => EnvVar.GetNullableOrDefault(ImagePullPolicyEnvVar);

        public PrometheiContractsContainerRecipe(DebugInfoVersion versionInfo)
        {
            this.versionInfo = versionInfo;
        }

        protected override void Initialize(StartupConfig startupConfig)
        {
            var setup = startupConfig.Get<PrometheiContractsSetup>();

            var address = setup.RpcNode.StartResult.Container.GetAddress(GethContainerRecipe.HttpPortTag);

            SetSchedulingAffinity(notIn: "false");

            AddEnvVar("DISTTEST_NETWORK_URL", address.ToString());

            // Default values:
            AddEnvVar("DISTTEST_REPAIRREWARD", 10);
            AddEnvVar("DISTTEST_MAXSLASHES", 2);
            AddEnvVar("DISTTEST_SLASHPERCENTAGE", 20);
            AddEnvVar("DISTTEST_VALIDATORREWARD", 20);
            AddEnvVar("DISTTEST_DOWNTIMEPRODUCT", 131);
            AddEnvVar("DISTTEST_MAXDURATION", Convert.ToInt32(TimeSpan.FromDays(30).TotalSeconds));

            if (setup.MaxReservationsOverride.HasValue)
            {
                AddEnvVar("DISTTEST_MAXRESERVATIONS", setup.MaxReservationsOverride.Value);
            }
            else
            {
                AddEnvVar("DISTTEST_MAXRESERVATIONS", 3);
            }

            // Customized values, required to operate in a network with
            // block frequency of 1.
            AddEnvVar("DISTTEST_PERIOD", PeriodSeconds);
            AddEnvVar("DISTTEST_TIMEOUT", TimeoutSeconds);
            AddEnvVar("DISTTEST_DOWNTIME", DowntimeSeconds);

            AddEnvVar("HARDHAT_NETWORK", "disttestnetwork");
            AddEnvVar("HARDHAT_IGNITION_CONFIRM_DEPLOYMENT", "false");
            AddEnvVar("KEEP_ALIVE", "1");
        }

        private string GetDefaultContractsDockerImage()
        {
            return $"durabilitylabs/promethei-contracts:sha-{versionInfo.Contracts}-dist-tests";
        }
    }
}
