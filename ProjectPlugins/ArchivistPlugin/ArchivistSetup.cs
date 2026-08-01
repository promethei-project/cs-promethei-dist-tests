using ArchivistClient;
using ArchivistContractsPlugin;
using GethPlugin;
using KubernetesWorkflow;
using Utils;

namespace ArchivistPlugin
{
    public interface IArchivistSetup
    {
        IArchivistSetup WithName(string name);
        IArchivistSetup WithImage(string img);
        IArchivistSetup At(ILocation location);
        IArchivistSetup WithBootstrapNode(IArchivistNode node);
        IArchivistSetup WithLogLevel(ArchivistLogLevel level);
        IArchivistSetup WithLogLevel(ArchivistLogLevel level, ArchivistLogCustomTopics customTopics);
        IArchivistSetup WithStorageQuota(ByteSize storageQuota);
        IArchivistSetup WithBlockTTL(TimeSpan duration);
        IArchivistSetup WithBlockMaintenanceInterval(TimeSpan duration);
        IArchivistSetup WithBlockMaintenanceNumber(int numberOfBlocks);
        IArchivistSetup WithFetchOrder(string fetchOrder);
        IArchivistSetup EnableMetrics();
        IArchivistSetup EnableMarketplace(IGethNode gethNode, IArchivistContracts archivistContracts, Action<IMarketplaceSetup> marketplaceSetup);
        /// <summary>
        /// Provides an invalid proof every N proofs
        /// </summary>
        IArchivistSetup WithSimulateProofFailures(uint failEveryNProofs);
    }

    public interface IMarketplaceSetup
    {
        IMarketplaceSetup WithInitial(Ether eth, TestToken tokens);
        IMarketplaceSetup WithAccount(EthAccount account);
        IMarketplaceSetup AsStorageNode();
        IMarketplaceSetup AsValidator();
    }

    public class ArchivistLogCustomTopics
    {
        public ArchivistLogCustomTopics(ArchivistLogLevel discV5, ArchivistLogLevel libp2p, ArchivistLogLevel blockExchange)
        {
            DiscV5 = discV5;
            Libp2p = libp2p;
            BlockExchange = blockExchange;
        }

        public ArchivistLogCustomTopics(ArchivistLogLevel discV5, ArchivistLogLevel libp2p)
        {
            DiscV5 = discV5;
            Libp2p = libp2p;
        }

        public ArchivistLogLevel DiscV5 { get; set; }
        public ArchivistLogLevel Libp2p { get; set; }
        public ArchivistLogLevel ContractClock { get; set; } = ArchivistLogLevel.Warn;
        public ArchivistLogLevel? BlockExchange { get; } = ArchivistLogLevel.Info;
        public ArchivistLogLevel JsonSerialize { get; set; } = ArchivistLogLevel.Warn;
        public ArchivistLogLevel MarketplaceInfra { get; set; } = ArchivistLogLevel.Warn;
    }

    public class ArchivistSetup : ArchivistStartupConfig, IArchivistSetup
    {
        public int NumberOfNodes { get; }

        public ArchivistSetup(ArchivistDockerImage dockerImage, int numberOfNodes)
        {
            Image = dockerImage.GetArchivistDockerImage();
            NumberOfNodes = numberOfNodes;
        }

        public IArchivistSetup WithName(string name)
        {
            NameOverride = name;
            return this;
        }

        public IArchivistSetup At(ILocation location)
        {
            Location = location;
            return this;
        }

        public IArchivistSetup WithBootstrapNode(IArchivistNode node)
        {
            BootstrapSpr = node.GetDebugInfo().Spr;
            return this;
        }

        public IArchivistSetup WithLogLevel(ArchivistLogLevel level)
        {
            LogLevel = level;
            return this;
        }

        public IArchivistSetup WithLogLevel(ArchivistLogLevel level, ArchivistLogCustomTopics customTopics)
        {
            LogLevel = level;
            CustomTopics = customTopics;
            return this;
        }

        public IArchivistSetup WithStorageQuota(ByteSize storageQuota)
        {
            StorageQuota = storageQuota;
            return this;
        }

        public IArchivistSetup WithBlockTTL(TimeSpan duration)
        {
            BlockTTL = duration;
            return this;
        }

        public IArchivistSetup WithBlockMaintenanceInterval(TimeSpan duration)
        {
            BlockMaintenanceInterval = duration;
            return this;
        }

        public IArchivistSetup WithBlockMaintenanceNumber(int numberOfBlocks)
        {
            BlockMaintenanceNumber = numberOfBlocks;
            return this;
        }

        public IArchivistSetup WithFetchOrder(string fetchOrder)
        {
            FetchOrder = fetchOrder;
            return this;
        }

        public IArchivistSetup EnableMetrics()
        {
            MetricsEnabled = true;
            return this;
        }

        public IArchivistSetup EnableMarketplace(IGethNode gethNode, IArchivistContracts archivistContracts, Action<IMarketplaceSetup> marketplaceSetup)
        {
            var ms = new MarketplaceSetup();
            marketplaceSetup(ms);

            MarketplaceConfig = new MarketplaceInitialConfig(ms, gethNode, archivistContracts);
            return this;
        }

        public IArchivistSetup WithSimulateProofFailures(uint failEveryNProofs)
        {
            SimulateProofFailures = failEveryNProofs;
            return this;
        }

        public string Describe()
        {
            var args = string.Join(',', DescribeArgs());
            var name = "";
            if (NameOverride != null)
            {
                name = $"'{NameOverride}' ";
            }

            return $"({NumberOfNodes} ArchivistNodes {name}with args:[{args}])";
        }

        private IEnumerable<string> DescribeArgs()
        {
            yield return $"LogLevel={LogLevelWithTopics()}";
            yield return $"Maintenance=(TTL={Time.FormatDuration(BlockTTL)}@{BlockMaintenanceNumber}/{Time.FormatDuration(BlockMaintenanceInterval)})";
            if (BootstrapSpr != null) yield return $"BootstrapNode={BootstrapSpr}";
            if (StorageQuota != null) yield return $"StorageQuota={StorageQuota}";
            if (SimulateProofFailures != null) yield return $"SimulateProofFailures={SimulateProofFailures}";
            if (MarketplaceConfig != null) yield return $"MarketplaceSetup={MarketplaceConfig.MarketplaceSetup}";
        }

        public IArchivistSetup WithImage(string img)
        {
            Image = img;
            return this;
        }
    }

    public class MarketplaceSetup : IMarketplaceSetup
    {
        public bool IsStorageNode { get; private set; }
        public bool IsValidator { get; private set; }
        public Ether InitialEth { get; private set; } = 0.Eth();
        public TestToken InitialTestTokens { get; private set; } = 0.Tst();
        public EthAccountSetup EthAccountSetup { get; } = new EthAccountSetup();

        public IMarketplaceSetup AsStorageNode()
        {
            IsStorageNode = true;
            return this;
        }

        public IMarketplaceSetup AsValidator()
        {
            IsValidator = true;
            return this;
        }

        public IMarketplaceSetup WithAccount(EthAccount account)
        {
            EthAccountSetup.Pin(account);
            return this;
        }

        public IMarketplaceSetup WithInitial(Ether eth, TestToken tokens)
        {
            InitialEth = eth;
            InitialTestTokens = tokens;
            return this;
        }

        public override string ToString()
        {
            var result = "[(clientNode)"; // When marketplace is enabled, being a clientNode is implicit.
            result += IsStorageNode ? "(storageNode)" : "()";
            result += IsValidator ? "(validator)" : "() ";
            result += $"Pinned address: '{EthAccountSetup}' ";
            result += $"{InitialEth} / {InitialTestTokens}";
            result += "] ";
            return result;
        }
    }

    public class EthAccountSetup
    {
        private readonly List<EthAccount> accounts = new List<EthAccount>();
        private bool pinned = false;

        public void Pin(EthAccount account)
        {
            accounts.Add(account);
            pinned = true;
        }

        public EthAccount GetNew()
        {
            if (pinned) return accounts.Last();

            var a = EthAccountGenerator.GenerateNew();
            accounts.Add(a);
            return a;
        }

        public EthAccount[] GetAll()
        {
            return accounts.ToArray();
        }

        public override string ToString()
        {
            if (!accounts.Any()) return "NoEthAccounts";
            return string.Join(",", accounts.Select(a => a.ToString()).ToArray());
        }
    }
}
