using PrometheiClient;
using PrometheiContractsPlugin;
using GethPlugin;
using KubernetesWorkflow.Types;

namespace PrometheiPlugin
{
    public class PrometheiDeployment
    {
        public PrometheiDeployment(PrometheiInstance[] prometheiInstances, GethDeployment gethDeployment,
            PrometheiContractsDeployment prometheiContractsDeployment, RunningPod? prometheusContainer,
            RunningPod? discordBotContainer, DeploymentMetadata metadata,
            string id)
        {
            Id = id;
            PrometheiInstances = prometheiInstances;
            GethDeployment = gethDeployment;
            PrometheiContractsDeployment = prometheiContractsDeployment;
            PrometheusContainer = prometheusContainer;
            DiscordBotContainer = discordBotContainer;
            Metadata = metadata;
        }

        public string Id { get; }
        public PrometheiInstance[] PrometheiInstances { get; }
        public GethDeployment GethDeployment { get; }
        public PrometheiContractsDeployment PrometheiContractsDeployment { get; }
        public RunningPod? PrometheusContainer { get; }
        public RunningPod? DiscordBotContainer { get; }
        public DeploymentMetadata Metadata { get; }
    }

    public class DeploymentMetadata
    {
        public DeploymentMetadata(string name, DateTime startUtc, DateTime finishedUtc, string kubeNamespace,
            int numberOfPrometheiNodes, int numberOfValidators, int storageQuotaMB, PrometheiLogLevel prometheiLogLevel,
            int initialTestTokens, int minPrice, int maxCollateral, int maxDuration, int blockTTL, int blockMI,
            int blockMN)
        {
            Name = name;
            StartUtc = startUtc;
            FinishedUtc = finishedUtc;
            KubeNamespace = kubeNamespace;
            NumberOfPrometheiNodes = numberOfPrometheiNodes;
            NumberOfValidators = numberOfValidators;
            StorageQuotaMB = storageQuotaMB;
            PrometheiLogLevel = prometheiLogLevel;
            InitialTestTokens = initialTestTokens;
            MinPrice = minPrice;
            MaxCollateral = maxCollateral;
            MaxDuration = maxDuration;
            BlockTTL = blockTTL;
            BlockMI = blockMI;
            BlockMN = blockMN;
        }

        public string Name { get; }
        public DateTime StartUtc { get; }
        public DateTime FinishedUtc { get; }
        public string KubeNamespace { get; }
        public int NumberOfPrometheiNodes { get; }
        public int NumberOfValidators { get; }
        public int StorageQuotaMB { get; }
        public PrometheiLogLevel PrometheiLogLevel { get; }
        public int InitialTestTokens { get; }
        public int MinPrice { get; }
        public int MaxCollateral { get; }
        public int MaxDuration { get; }
        public int BlockTTL { get; }
        public int BlockMI { get; }
        public int BlockMN { get; }
    }
}
