using System.Net.Sockets;
using System.Net;
using Nethereum.Util;

namespace ArchivistPlugin
{
    public class ProcessRecipe
    {
        public ProcessRecipe(string cmd, string[] args)
        {
            Cmd = cmd;
            Args = args;
        }

        public string Cmd { get; }
        public string[] Args { get; }
    }

    public class ArchivistProcessConfig
    {
        public ArchivistProcessConfig(string name, FreePortFinder freePortFinder, string dataDir)
        {
            ApiPort = freePortFinder.GetNextFreePort();
            DiscPort = freePortFinder.GetNextFreePort();
            ListenPort = freePortFinder.GetNextFreePort();
            Name = name;
            DataDir = dataDir;
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var addrs = host.AddressList.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToList();

            LocalIpAddrs = addrs.First();
        }

        public int ApiPort { get; }
        public int DiscPort { get; }
        public int ListenPort { get; }
        public string Name { get; }
        public string DataDir { get; }
        public IPAddress LocalIpAddrs { get; }
    }

    public class ArchivistProcessRecipe
    {
        private readonly ArchivistProcessConfig pc;
        private readonly ArchivistExePath archivistExePath;

        public ArchivistProcessRecipe(ArchivistProcessConfig pc, ArchivistExePath archivistExePath)
        {
            this.pc = pc;
            this.archivistExePath = archivistExePath;
        }

        public ProcessRecipe Initialize(ArchivistStartupConfig config)
        {
            args.Clear();

            AddArg("--api-port", pc.ApiPort);
            AddArg("--api-bindaddr", "0.0.0.0");

            AddArg("--data-dir", pc.DataDir);

            AddArg("--disc-port", pc.DiscPort);
            AddArg("--log-level", config.LogLevelWithTopics());

            // This makes the node announce itself to its local IP address.
            AddArg("--nat", $"extip:{pc.LocalIpAddrs.ToStringInvariant()}");

            AddArg("--listen-addrs", $"/ip4/0.0.0.0/tcp/{pc.ListenPort}");

            if (!string.IsNullOrEmpty(config.BootstrapSpr))
            {
                AddArg("--bootstrap-node", config.BootstrapSpr);
            }
            if (config.StorageQuota != null)
            {
                AddArg("--storage-quota", config.StorageQuota.SizeInBytes.ToString()!);
            }
            if (config.BlockTTL != null)
            {
                AddArg("--overlay-ttl", config.BlockTTL.Value.TotalSeconds.ToString());
            }
            if (config.BlockMaintenanceInterval != null)
            {
                AddArg("--block-mi", Convert.ToInt32(config.BlockMaintenanceInterval.Value.TotalSeconds).ToString());
            }
            if (config.BlockMaintenanceNumber != null)
            {
                AddArg("--block-mn", config.BlockMaintenanceNumber.ToString()!);
            }
            if (!string.IsNullOrEmpty(config.FetchOrder))
            {
                AddArg("--fetch-order", config.FetchOrder);
            }
            var fsyncFile = config.FsyncFile ?? GetEnvBool("ARCHIVIST_FSYNC_FILE");
            var fsyncDir = config.FsyncDir ?? GetEnvBool("ARCHIVIST_FSYNC_DIR");
            if (fsyncFile.HasValue)
            {
                AddArg("--fs-fsync-file", fsyncFile.Value.ToString().ToLowerInvariant());
            }
            if (fsyncDir.HasValue)
            {
                AddArg("--fs-fsync-dir", fsyncDir.Value.ToString().ToLowerInvariant());
            }
            if (config.MetricsEnabled)
            {
                throw new Exception("Not supported");
                //var metricsPort = CreateApiPort(config, MetricsPortTag);
                //AddEnvVar("ARCHIVIST_METRICS", "true");
                //AddEnvVar("ARCHIVIST_METRICS_ADDRESS", "0.0.0.0");
                //AddEnvVar("ARCHIVIST_METRICS_PORT", metricsPort);
                //AddPodAnnotation("prometheus.io/scrape", "true");
                //AddPodAnnotation("prometheus.io/port", metricsPort.Number.ToString());
            }

            if (config.SimulateProofFailures != null)
            {
                throw new Exception("Not supported");
                //AddEnvVar("ARCHIVIST_SIMULATE_PROOF_FAILURES", config.SimulateProofFailures.ToString()!);
            }

            if (config.MarketplaceConfig != null)
            {
                throw new Exception("Not supported");
                //var mconfig = config.MarketplaceConfig;
                //var gethStart = mconfig.GethNode.StartResult;
                //var wsAddress = gethStart.Container.GetInternalAddress(GethContainerRecipe.WsPortTag);
                //var marketplaceAddress = mconfig.ArchivistContracts.Deployment.MarketplaceAddress;

                //AddEnvVar("ARCHIVIST_ETH_PROVIDER", $"{wsAddress.Host.Replace("http://", "ws://")}:{wsAddress.Port}");
                //AddEnvVar("ARCHIVIST_MARKETPLACE_ADDRESS", marketplaceAddress);

                //var marketplaceSetup = config.MarketplaceConfig.MarketplaceSetup;

                //// Custom scripting in the Archivist test image will write this variable to a private-key file,
                //// and pass the correct filename to Archivist.
                //var account = marketplaceSetup.EthAccountSetup.GetNew();
                //AddEnvVar("ETH_PRIVATE_KEY", account.PrivateKey);
                //Additional(account);

                //SetCommandOverride(marketplaceSetup);
                //if (marketplaceSetup.IsValidator)
                //{
                //    AddEnvVar("ARCHIVIST_VALIDATOR", "true");
                //}
            }

            //if (!string.IsNullOrEmpty(config.NameOverride))
            //{
            //    AddEnvVar("ARCHIVIST_NODENAME", config.NameOverride);
            //}

            return Create();
        }

        private ProcessRecipe Create()
        {
            return new ProcessRecipe(
                cmd: archivistExePath.Get(),
                args: args.ToArray());
        }

        private readonly List<string> args = new List<string>();

        private void AddArg(string arg, string val)
        {
            args.Add($"{arg}={val}");
        }

        private void AddArg(string arg, int val)
        {
            args.Add($"{arg}={val}");
        }

        private static bool? GetEnvBool(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value)) return null;
            return value.ToLowerInvariant() == "true" || value == "1";
        }
    }
}
