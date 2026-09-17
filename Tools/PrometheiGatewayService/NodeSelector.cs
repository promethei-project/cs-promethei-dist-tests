using Logging;

namespace PrometheiGatewayService
{
    public class NodeSelector
    {
        private readonly ILog log;
        private readonly Configuration config;
        private readonly object _mapLock = new object();
        private readonly List<string> nodeEndpoints = new();
        private readonly Dictionary<string, string> knownMappings = new Dictionary<string, string>();

        public NodeSelector(ILog log, Configuration config)
        {
            this.log = new LogPrefixer(log, "(NodeSelector)");
            this.config = config;
        }

        public async Task Initialize()
        {
            Log("Initializing...");

            nodeEndpoints.AddRange(config.PrometheiEndpoints
                .Split(";", StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.TrimEnd('/')));

            if (!nodeEndpoints.Any()) throw new Exception("No Promethei endpoints configured");

            using var client = new HttpClient();
            foreach (var n in nodeEndpoints) await CheckEndpoint(n, client);

            Log("Ready");
        }

        public string GetNodeUrl(string cid)
        {
            var endpoint = GetEndpointFor(cid);
            return $"{endpoint}/api/promethei/v1/";
        }

        public async Task CheckOneNode()
        {
            var endpoint = "";
            lock (_mapLock)
            {
                // Cycle through the endpoints.
                endpoint = nodeEndpoints[0];
                nodeEndpoints.RemoveAt(0);
                nodeEndpoints.Add(endpoint);
            }

            using var client = new HttpClient();
            await CheckEndpoint(endpoint, client);
        }

        private async Task CheckEndpoint(string endpoint, HttpClient client)
        {
            var infoUrl = $"{endpoint}/api/promethei/v1/debug/info";
            Log($"Checking node at '{infoUrl}'...");

            var response = await client.GetAsync(infoUrl);
            var str = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(str))
            {
                Log($"Invalid response received");
                throw new Exception($"Invalid response received from endpoint '{endpoint}'");
            }
        }

        private string GetEndpointFor(string cid)
        {
            // If the CID was previously assigned to a node,
            // use the same node for the same CID.
            // It might still have (some of) the data.
            lock (_mapLock)
            {
                if (knownMappings.TryGetValue(cid, out string? value))
                {
                    return value;
                }
                // Clean up mappings if there are too many.
                if (knownMappings.Count > 1000)
                {
                    knownMappings.Clear();
                    Log("Cleared all mappings.");
                }

                // Cycle through the endpoints and assign them.
                var endpoint = nodeEndpoints[0];
                nodeEndpoints.RemoveAt(0);
                nodeEndpoints.Add(endpoint);
                knownMappings.Add(cid, endpoint);
                Log($"CID '{cid}' mapped to endpoint '{endpoint}'");
                return endpoint;
            }
        }

        private void Log(string msg)
        {
            log.Log(msg);
        }
    }
}
