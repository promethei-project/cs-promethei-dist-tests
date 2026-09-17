namespace PrometheiNetworkConfig
{
    public class PrometheiNetwork
    {
        public string Name { get; set; } = string.Empty;
        public PrometheiVersionEntry Version { get; set; } = new PrometheiVersionEntry();
        public PrometheiSprEntry SPR { get; set; } = new PrometheiSprEntry();
        public string[] RPCs { get; set; } = Array.Empty<string>();
        public PrometheiMarketplaceEntry Marketplace { get; set; } = new PrometheiMarketplaceEntry();
        public TeamObject Team { get; set; } = new TeamObject();
    }

    public class TeamObject
    {
        public TeamNodesCategory[] Nodes { get; set; } = Array.Empty<TeamNodesCategory>();
        public PrometheiNetworkTeamUtilsObject Utils { get; set; } = new PrometheiNetworkTeamUtilsObject();

        public Dictionary<string, string> GetNodesAsLogReplacements()
        {
            var result = new Dictionary<string, string>();
            foreach (var node in Nodes)
            {
                foreach (var instance in node.Instances)
                {
                    result.Add(instance.EthAddress, instance.Name);
                }
            }
            return result;
        }
    }

    public class TeamNodesCategory
    {
        public string Category { get; set; } = string.Empty;
        public PrometheiNetworkTeamNodesVersionsInstancesEntry[] Instances { get; set; } = Array.Empty<PrometheiNetworkTeamNodesVersionsInstancesEntry>();
    }
}
