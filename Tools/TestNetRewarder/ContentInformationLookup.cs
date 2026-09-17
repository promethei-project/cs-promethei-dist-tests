using PrometheiClient;
using PrometheiNetworkConfig;
using Utils;

namespace TestNetRewarder
{
    public class ContentInformationLookup
    {
        private readonly GatewayClient.GatewayClient client;
        private readonly Configuration config;
        private readonly PrometheiNetwork network;
        private readonly Dictionary<string, Manifest> cache = new Dictionary<string, Manifest>();

        public ContentInformationLookup(Configuration config, PrometheiNetwork network)
        {
            client = new GatewayClient.GatewayClient(network);
            this.config = config;
            this.network = network;
        }

        public string[] DescribeManifest(string cid)
        {
            return Safe(cid, LookupContent);
        }

        public string GenerateDownloadLink(string cid)
        {
            return string.Join("", Safe(cid, GetDownloadLink));
        }

        private string[] LookupContent(string cid)
        {
            var manifest = GetManifest(cid);
            return
            [
                $"   Filename: '{manifest.Filename}'",
                $"   ContentType: {manifest.Mimetype}",
                $"   DatasetSize: {manifest.DatasetSize}"
            ];
        }

        private string[] GetDownloadLink(string cid)
        {
            var manifest = GetManifest(cid);
            if (manifest.DatasetSize.SizeInBytes > config.DownloadLinkMaxSizeMb.MB().SizeInBytes) return Array.Empty<string>();
            var downloadLink = $"{network.Team.Utils.Gateway}/{cid}";
            return
            [
                $"[Download via Gateway](<{downloadLink}>)"
            ];
        }

        private string[] Safe(string cid, Func<string, string[]> operation)
        {
            if (string.IsNullOrEmpty(cid)) return Array.Empty<string>();

            try
            {
                return operation(cid);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private Manifest GetManifest(string cid)
        {
            if (cache.TryGetValue(cid, out Manifest? value)) return value;
            
            if (cache.Count > 1000) cache.Clear();

            var manifest = client.GetManifest(cid).Manifest;
            cache.Add(cid, manifest);

            return manifest;
        }
    }
}
