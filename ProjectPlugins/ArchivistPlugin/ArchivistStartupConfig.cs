using ArchivistClient;
using KubernetesWorkflow;
using Utils;

namespace ArchivistPlugin
{
    public class ArchivistStartupConfig
    {
        public string? NameOverride { get; set; }
        public ILocation Location { get; set; } = KnownLocations.UnspecifiedLocation;
        public ArchivistLogLevel LogLevel { get; set; }
        public ArchivistLogCustomTopics? CustomTopics { get; set; } = new ArchivistLogCustomTopics(ArchivistLogLevel.Info, ArchivistLogLevel.Warn);
        public ByteSize? StorageQuota { get; set; }
        public bool MetricsEnabled { get; set; } = true;
        public MarketplaceInitialConfig? MarketplaceConfig { get; set; }
        public string? BootstrapSpr { get; set; }
        public TimeSpan? BlockTTL { get; set; }
        public uint? SimulateProofFailures { get; set; }
        public bool? EnableValidator { get; set; }
        public TimeSpan? BlockMaintenanceInterval { get; set; }
        public int? BlockMaintenanceNumber { get; set; }
        public string? FetchOrder { get; set; }
        public bool? FsyncFile { get; set; }
        public bool? FsyncDir { get; set; }
        public string Image { get; set; } = string.Empty;

        public string LogLevelWithTopics()
        {
            var level = LogLevel.ToString()!.ToUpperInvariant();
            if (CustomTopics != null)
            {
                var discV5Topics = new[]
                {
                    "discv5",
                    "providers",
                    "routingtable",
                    "manager",
                    "cache",
                };
                var libp2pTopics = new[]
                {
                    "libp2p",
                    "multistream",
                    "switch",
                    "transport",
                    "tcptransport",
                    "semaphore",
                    "asyncstreamwrapper",
                    "lpstream",
                    "mplex",
                    "mplexchannel",
                    "noise",
                    "bufferstream",
                    "mplexcoder",
                    "secure",
                    "chronosstream",
                    "connection",
                    // Removed: "connmanager", is used for transcript peer-dropped event.
                    "websock",
                    "ws-session",
                    // Removed: "dialer", is used for transcript successful-dial event.
                    "muxedupgrade",
                    "upgrade",
                    "identify"
                };
                var blockExchangeTopics = new[]
                {
                    "archivist",
                    "pendingblocks",
                    "peerctxstore",
                    "discoveryengine",
                    "blockexcengine",
                    "blockexcnetwork",
                    "blockexcnetworkpeer"
                };
                var contractClockTopics = new[]
                {
                    "contracts",
                    "clock"
                };
                var jsonSerializeTopics = new[]
                {
                    "serde",
                    "json",
                    "serialization"
                };
                var marketplaceInfraTopics = new[]
                {
                    "JSONRPC-WS-CLIENT",
                    "JSONRPC-HTTP-CLIENT",
                };

                var alwaysIgnoreTopics = new []
                {
                    "JSONRPC-CLIENT"
                };

                level = $"{level};" +
                    $"{CustomTopics.DiscV5.ToString()!.ToLowerInvariant()}:{string.Join(",", discV5Topics)};" +
                    $"{CustomTopics.Libp2p.ToString()!.ToLowerInvariant()}:{string.Join(",", libp2pTopics)};" +
                    $"{CustomTopics.ContractClock.ToString().ToLowerInvariant()}:{string.Join(",", contractClockTopics)};" +
                    $"{CustomTopics.JsonSerialize.ToString().ToLowerInvariant()}:{string.Join(",", jsonSerializeTopics)};" +
                    $"{CustomTopics.MarketplaceInfra.ToString().ToLowerInvariant()}:{string.Join(",", marketplaceInfraTopics)};" +
                    $"{ArchivistLogLevel.Error.ToString()}:{string.Join(",", alwaysIgnoreTopics)}";

                if (CustomTopics.BlockExchange != null)
                {
                    level += $";{CustomTopics.BlockExchange.ToString()!.ToLowerInvariant()}:{string.Join(",", blockExchangeTopics)}";
                }
            }
            return level;
        }
    }
}
