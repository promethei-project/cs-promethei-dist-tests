using GethPlugin;
using KubernetesWorkflow;
using KubernetesWorkflow.Recipe;
using Utils;

namespace ArchivistPlugin
{
    public class ArchivistContainerRecipe : ContainerRecipeFactory
    {
        public const string ApiPortTag = "archivist_api_port";
        public const string ListenPortTag = "archivist_listen_port";
        public const string MetricsPortTag = "archivist_metrics_port";
        public const string DiscoveryPortTag = "archivist_discovery_port";

        // Used by tests for time-constraint assertions.
        public static readonly TimeSpan MaxUploadTimePerMegabyte = TimeSpan.FromSeconds(2.0);
        public static readonly TimeSpan MaxDownloadTimePerMegabyte = TimeSpan.FromSeconds(2.0);
        //private readonly ArchivistDockerImage archivistDockerImage;

        private string image = string.Empty;

        public override string AppName => "archivist";
        public override string Image => image;
        public override string? ImagePullPolicy => EnvVar.GetNullableOrDefault("ARCHIVIST_IMAGE_PULL_POLICY");

        protected override void Initialize(StartupConfig startupConfig)
        {
            SetResourcesRequest(milliCPUs: 100, memory: 100.MB());
            //SetResourceLimits(milliCPUs: 4000, memory: 12.GB());

            SetSchedulingAffinity(notIn: "false");
            SetSystemCriticalPriority();

            var config = startupConfig.Get<ArchivistStartupConfig>();

            image = config.Image;
            if (string.IsNullOrEmpty(image)) throw new Exception("A!");

            var apiPort = CreateApiPort();
            AddEnvVar("ARCHIVIST_API_PORT", apiPort);
            AddEnvVar("ARCHIVIST_API_BINDADDR", "0.0.0.0");

            var dataDir = $"datadir{ContainerNumber}";
            AddEnvVar("ARCHIVIST_DATA_DIR", dataDir);
            AddVolume($"archivist/{dataDir}", GetVolumeCapacity(config));

            var discPort = CreateDiscoveryPort();
            AddEnvVar("ARCHIVIST_DISC_PORT", discPort);
            AddEnvVar("ARCHIVIST_LOG_LEVEL", config.LogLevelWithTopics());

            // This makes the node announce itself to its local (pod) IP address.
            AddEnvVar("NAT_IP_AUTO", "true");

            var listenPort = CreateListenPort();
            AddEnvVar("ARCHIVIST_LISTEN_ADDRS", $"/ip4/0.0.0.0/tcp/{listenPort.Number}");

            if (!string.IsNullOrEmpty(config.BootstrapSpr))
            {
                AddEnvVar("ARCHIVIST_BOOTSTRAP_NODE", config.BootstrapSpr);
            }
            if (config.StorageQuota != null)
            {
                AddEnvVar("ARCHIVIST_STORAGE_QUOTA", config.StorageQuota.SizeInBytes.ToString()!);
            }
            if (config.BlockTTL != null)
            {
                AddEnvVar("ARCHIVIST_OVERLAY_TTL", Convert.ToInt32(config.BlockTTL.Value.TotalSeconds).ToString());
            }
            if (config.BlockMaintenanceInterval != null)
            {
                AddEnvVar("ARCHIVIST_BLOCK_MI", Convert.ToInt32(config.BlockMaintenanceInterval.Value.TotalSeconds).ToString());
            }
            if (config.BlockMaintenanceNumber != null)
            {
                AddEnvVar("ARCHIVIST_BLOCK_MN", config.BlockMaintenanceNumber.ToString()!);
            }
            if (!string.IsNullOrEmpty(config.FetchOrder))
            {
                AddEnvVar("ARCHIVIST_FETCH_ORDER", config.FetchOrder);
            }
            if (config.MetricsEnabled)
            {
                var metricsPort = AddExposedPort(MetricsPortTag);
                AddEnvVar("ARCHIVIST_METRICS", "true");
                AddEnvVar("ARCHIVIST_METRICS_ADDRESS", "0.0.0.0");
                AddEnvVar("ARCHIVIST_METRICS_PORT", metricsPort);
                AddPodAnnotation("prometheus.io/scrape", "true");
                AddPodAnnotation("prometheus.io/port", metricsPort.Number.ToString());
            }

            if (config.SimulateProofFailures != null)
            {
                AddEnvVar("ARCHIVIST_SIMULATE_PROOF_FAILURES", config.SimulateProofFailures.ToString()!);
            }

            if (config.MarketplaceConfig != null)
            {
                AddEnvVar("ARCHIVIST_PERSISTENCE", "true");

                var mconfig = config.MarketplaceConfig;
                var gethStart = mconfig.GethNode.StartResult;
                var marketplaceAddress = mconfig.ArchivistContracts.Deployment.MarketplaceAddress;

                AddEnvVar("ARCHIVIST_ETH_PROVIDER", GetEthRpcProvider(gethStart, mconfig));
                AddEnvVar("ARCHIVIST_MARKETPLACE_ADDRESS", marketplaceAddress.Address);

                var marketplaceSetup = config.MarketplaceConfig.MarketplaceSetup;

                // Custom scripting in the Archivist test image will write this variable to a private-key file,
                // and pass the correct filename to Archivist.
                var account = marketplaceSetup.EthAccountSetup.GetNew();
                AddEnvVar("ETH_PRIVATE_KEY", account.PrivateKey);
                Additional(account);

                if (marketplaceSetup.IsStorageNode)
                {
                    AddEnvVar("ARCHIVIST_PROVER", "true");
                }
                if (marketplaceSetup.IsValidator)
                {
                   AddEnvVar("ARCHIVIST_VALIDATOR", "true");
                }
            }

            // Required for the new "overlay" support:
            AddEnvVar("ARCHIVIST_FS_FSYNC_FILE", "off");
            AddEnvVar("ARCHIVIST_FS_FSYNC_DIR", "off");

            if (!string.IsNullOrEmpty(config.NameOverride))
            {
                AddEnvVar("ARCHIVIST_NODENAME", config.NameOverride);
            }
        }

        private string GetEthRpcProvider(GethDeployment gethStart, MarketplaceInitialConfig mconfig)
        {
            if (mconfig.UseWebsocketRPC)
            {
                var wsAddress = gethStart.Container.GetInternalAddress(GethContainerRecipe.WsPortTag);
                return $"{wsAddress.Host.Replace("http://", "ws://")}:{wsAddress.Port}";
            }
            var httpAddress = gethStart.Container.GetInternalAddress(GethContainerRecipe.HttpPortTag);
            return $"{httpAddress.Host}:{httpAddress.Port}";
        }

        private Port CreateApiPort()
        {
            return AddExposedPort(ApiPortTag);
        }

        private Port CreateListenPort()
        {
            return AddInternalPort(ListenPortTag);
        }

        private Port CreateDiscoveryPort()
        {
            return AddInternalPort(DiscoveryPortTag, PortProtocol.UDP);
        }

        private ByteSize GetVolumeCapacity(ArchivistStartupConfig config)
        {
            if (config.StorageQuota != null) return config.StorageQuota.Multiply(1.2);
            // Default Archivist quota: 8 Gb, using +20% to be safe.
            return 8.GB().Multiply(1.2);
        }
    }
}
