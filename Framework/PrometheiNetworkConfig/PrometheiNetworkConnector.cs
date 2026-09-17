using Logging;
using Newtonsoft.Json;
using Utils;

namespace PrometheiNetworkConfig
{
    public class PrometheiNetworkConnector
    {
        /// <summary>
        /// Promethei network for which information is fetched.
        /// Optional. Default: "testnet"
        /// </summary>
        public static string EnvVar_Network = "PROMETHEI_NETWORK";

        /// <summary>
        /// Promethei version for which information is fetched.
        /// Optional. Default: "latest"
        /// </summary>
        public static string EnvVar_Version = "PROMETHEI_VERSION";

        /// <summary>
        /// Optional override. If set, use this URL to fetch config JSON.
        /// </summary>
        private const string EnvVar_ConfigUrl = "PROMETHEI_CONFIG_URL";

        /// <summary>
        /// Optional override. If set, use this filepath to read config JSON.
        /// </summary>
        private const string EnvVar_ConfigFile = "PROMETHEI_CONFIG_FILE";

        private readonly string network;
        private readonly string version;
        private readonly ILog log;
        private PrometheiNetwork? model = null;

        public PrometheiNetworkConnector(ILog log)
            : this(
                log,
                EnvVar.GetOrDefault("PROMETHEI_NETWORK", "testnet"),
                EnvVar.GetOrDefault("PROMETHEI_VERSION", "latest")
            )
        {
        }

        public PrometheiNetworkConnector(ILog log, string network, string version)
        {
            this.log = new LogPrefixer(log, $"({nameof(PrometheiNetworkConnector)})");
            this.network = network.ToLowerInvariant();
            this.version = version.ToLowerInvariant();
        }

        public PrometheiNetwork GetConfig()
        {
            if (model == null)
            {
                var retry = new Retry(nameof(FetchModel),
                    maxTimeout: TimeSpan.FromMinutes(5.0),
                    sleepAfterFail: TimeSpan.FromSeconds(10.0),
                    onFail: f => { },
                    failFast: false);

                var fullModel = retry.Run(FetchModel);
                model = MapToVersion(fullModel);
                log.Log("Success");
            }
            return model;
        }

        private NetworkConfig FetchModel()
        {
            var str = FetchModelJson();
            return JsonConvert.DeserializeObject<NetworkConfig>(str)!;
        }

        private string FetchModelJson()
        {
            var overrideFile = EnvVar.GetOrDefault(EnvVar_ConfigFile, string.Empty);
            if (!string.IsNullOrEmpty(overrideFile))
            {
                return FetchModelFromFile(overrideFile);
            }

            return FetchModelFromUrl();
        }

        private string FetchModelFromFile(string overrideFile)
        {
            log.Log($"Loading from file '{overrideFile}' ...");
            return File.ReadAllText(overrideFile);
        }

        private string FetchModelFromUrl()
        {
            using var client = new HttpClient();
            var url = GetFetchUrl();
            log.Log($"Loading from URL '{url}' ...");
            var response = Time.Wait(client.GetAsync(url));
            return Time.Wait(response.Content.ReadAsStringAsync());
        }

        private string? GetFetchUrl()
        {
            var overrideUrl = EnvVar.GetOrDefault(EnvVar_ConfigUrl, string.Empty);
            if (!string.IsNullOrEmpty(overrideUrl)) return overrideUrl;
            return $"https://config.archivist.storage/{network}.json";
        }

        private PrometheiNetwork MapToVersion(NetworkConfig fullModel)
        {
            var selected = GetVersion(fullModel);

            return new PrometheiNetwork
            {
                Name = network,
                Version = fullModel.Promethei.Single(v => v.Version == selected),
                SPR = fullModel.SPRs.First(s => s.SupportedVersions.Contains(selected)),
                RPCs = fullModel.RPCs,
                Marketplace = fullModel.Marketplace.First(m => m.SupportedVersions.Contains(selected)),
                Team = MapToVersion(fullModel.Team, selected)
            };
        }

        private TeamObject MapToVersion(PrometheiNetworkTeamObject team, string selected)
        {
            return new TeamObject
            {
                Nodes = MapToVersion(team.Nodes, selected),
                Utils = team.Utils
            };
        }

        private TeamNodesCategory[] MapToVersion(PrometheiNetworkTeamNodesEntry[] nodes, string selected)
        {
            return nodes.Select(n => MapToVersion(n, selected)).ToArray();
        }

        private TeamNodesCategory MapToVersion(PrometheiNetworkTeamNodesEntry n, string selected)
        {
            return new TeamNodesCategory
            {
                Category = n.Category,
                Instances = n.Versions.Single(v => v.Version == selected).Instances
            };
        }

        private string GetVersion(NetworkConfig fullModel)
        {
            if (version == "latest")
            {
                return fullModel.Latest;
            }
            return version;
        }
    }
}
