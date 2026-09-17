namespace PrometheiNetworkConfig
{
    [Serializable]
    public class NetworkConfig
    {
        public string Latest { get; set; } = string.Empty;
        public PrometheiVersionEntry[] Promethei { get; set; } = Array.Empty<PrometheiVersionEntry>();
        public PrometheiSprEntry[] SPRs { get; set; } = Array.Empty<PrometheiSprEntry>();
        public string[] RPCs { get; set; } = Array.Empty<string>();
        public PrometheiMarketplaceEntry[] Marketplace { get; set; } = Array.Empty<PrometheiMarketplaceEntry>();
        public PrometheiNetworkTeamObject Team { get; set; } = new PrometheiNetworkTeamObject();
    }

    [Serializable]
    public class PrometheiVersionEntry
    {
        public string Version { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public string Contracts { get; set; } = string.Empty;
    }

    [Serializable]
    public class PrometheiSprEntry
    {
        public string[] SupportedVersions { get; set; } = Array.Empty<string>();
        public string[] Records { get; set; } = Array.Empty<string>();
    }

    [Serializable]
    public class PrometheiMarketplaceEntry
    {
        public string[] SupportedVersions { get; set; } = Array.Empty<string>();
        public string ContractAddress { get; set; } = string.Empty;
        public string ABI { get; set; } = string.Empty;
    }

    [Serializable]
    public class PrometheiNetworkTeamObject
    {
        public PrometheiNetworkTeamNodesEntry[] Nodes { get; set; } = Array.Empty<PrometheiNetworkTeamNodesEntry>();
        public PrometheiNetworkTeamUtilsObject Utils { get; set; } = new PrometheiNetworkTeamUtilsObject();
    }

    [Serializable]
    public class PrometheiNetworkTeamNodesEntry
    {
        public string Category { get; set; } = string.Empty;
        public PrometheiNetworkTeamNodesVersionsEntry[] Versions { get; set; } = Array.Empty<PrometheiNetworkTeamNodesVersionsEntry>();
    }

    [Serializable]
    public class PrometheiNetworkTeamNodesVersionsEntry
    {
        public string Version { get; set; } = string.Empty;
        public PrometheiNetworkTeamNodesVersionsInstancesEntry[] Instances { get; set; } = Array.Empty<PrometheiNetworkTeamNodesVersionsInstancesEntry>();
    }

    [Serializable]
    public class PrometheiNetworkTeamNodesVersionsInstancesEntry
    {
        public string Name { get; set; } = string.Empty;
        public string PodName { get; set; } = string.Empty;
        public string EthAddress { get; set; } = string.Empty;
    }

    [Serializable]
    public class PrometheiNetworkTeamUtilsObject
    {
        public string CrawlerRpc { get; set; } = string.Empty;
        public string BotRpc { get; set; } = string.Empty;
        public string ElasticSearch { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string TransactionLinkFormat { get; set; } = string.Empty;
    }
}
