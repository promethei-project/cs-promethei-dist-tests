using PrometheiClient;
using PrometheiContractsPlugin;
using GethPlugin;
using KubernetesWorkflow;
using Utils;

namespace PrometheiPlugin
{
    public interface IPrometheiSetup
    {
        IPrometheiSetup WithName(string name);
        IPrometheiSetup WithImage(string img);
        IPrometheiSetup At(ILocation location);
        IPrometheiSetup WithBootstrapNode(IPrometheiNode node);
        IPrometheiSetup WithLogLevel(PrometheiLogLevel level);
        IPrometheiSetup WithLogLevel(PrometheiLogLevel level, PrometheiLogCustomTopics customTopics);
        IPrometheiSetup WithStorageQuota(ByteSize storageQuota);
        IPrometheiSetup WithBlockTTL(TimeSpan duration);
        IPrometheiSetup WithBlockMaintenanceInterval(TimeSpan duration);
        IPrometheiSetup WithBlockMaintenanceNumber(int numberOfBlocks);
        IPrometheiSetup WithFetchOrder(string fetchOrder);
        IPrometheiSetup EnableMetrics();
        IPrometheiSetup EnableMarketplace(IGethNode gethNode, IPrometheiContracts prometheiContracts, Action<IMarketplaceSetup> marketplaceSetup);
        /// <summary>
        /// Provides an invalid proof every N proofs
        /// </summary>
        IPrometheiSetup WithSimulateProofFailures(uint failEveryNProofs);
    }

    public interface IMarketplaceSetup
    {
        IMarketplaceSetup WithInitial(Ether eth, TestToken tokens);
        IMarketplaceSetup WithAccount(EthAccount account);
        IMarketplaceSetup AsStorageNode();
        IMarketplaceSetup AsValidator();
    }

    public class PrometheiLogCustomTopics
    {
        public PrometheiLogCustomTopics(PrometheiLogLevel discV5, PrometheiLogLevel libp2p, PrometheiLogLevel blockExchange)
        {
            DiscV5 = discV5;
            Libp2p = libp2p;
            BlockExchange = blockExchange;
        }

        public PrometheiLogCustomTopics(PrometheiLogLevel discV5, PrometheiLogLevel libp2p)
        {
            DiscV5 = discV5;
            Libp2p = libp2p;
        }

        public PrometheiLogLevel DiscV5 { get; set; }
        public PrometheiLogLevel Libp2p { get; set; }
        public PrometheiLogLevel ContractClock { get; set; } = PrometheiLogLevel.Warn;
        public PrometheiLogLevel? BlockExchange { get; } = PrometheiLogLevel.Info;
        public PrometheiLogLevel JsonSerialize { get; set; } = PrometheiLogLevel.Warn;
        public PrometheiLogLevel MarketplaceInfra { get; set; } = PrometheiLogLevel.Warn;
    }

    public class PrometheiSetup : PrometheiStartupConfig, IPrometheiSetup
    {
        public int NumberOfNodes { get; }

        public PrometheiSetup(PrometheiDockerImage dockerImage, int numberOfNodes)
        {
            Image = dockerImage.GetPrometheiDockerImage();
            NumberOfNodes = numberOfNodes;
        }

        public IPrometheiSetup WithName(string name)
        {
            NameOverride = name;
            return this;
        }

        public IPrometheiSetup At(ILocation location)
        {
            Location = location;
            return this;
        }

        public IPrometheiSetup WithBootstrapNode(IPrometheiNode node)
        {
            BootstrapSpr = node.GetDebugInfo().Spr;
            return this;
        }

        public IPrometheiSetup WithLogLevel(PrometheiLogLevel level)
        {
            LogLevel = level;
            return this;
        }

        public IPrometheiSetup WithLogLevel(PrometheiLogLevel level, PrometheiLogCustomTopics customTopics)
        {
            LogLevel = level;
            CustomTopics = customTopics;
            return this;
        }

        public IPrometheiSetup WithStorageQuota(ByteSize storageQuota)
        {
            StorageQuota = storageQuota;
            return this;
        }

        public IPrometheiSetup WithBlockTTL(TimeSpan duration)
        {
            BlockTTL = duration;
            return this;
        }

        public IPrometheiSetup WithBlockMaintenanceInterval(TimeSpan duration)
        {
            BlockMaintenanceInterval = duration;
            return this;
        }

        public IPrometheiSetup WithBlockMaintenanceNumber(int numberOfBlocks)
        {
            BlockMaintenanceNumber = numberOfBlocks;
            return this;
        }

        public IPrometheiSetup WithFetchOrder(string fetchOrder)
        {
            FetchOrder = fetchOrder;
            return this;
        }

        public IPrometheiSetup EnableMetrics()
        {
            MetricsEnabled = true;
            return this;
        }

        public IPrometheiSetup EnableMarketplace(IGethNode gethNode, IPrometheiContracts prometheiContracts, Action<IMarketplaceSetup> marketplaceSetup)
        {
            var ms = new MarketplaceSetup();
            marketplaceSetup(ms);

            MarketplaceConfig = new MarketplaceInitialConfig(ms, gethNode, prometheiContracts);
            return this;
        }

        public IPrometheiSetup WithSimulateProofFailures(uint failEveryNProofs)
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

            return $"({NumberOfNodes} PrometheiNodes {name}with args:[{args}])";
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

        public IPrometheiSetup WithImage(string img)
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
