using PrometheiClient;
using PrometheiNetworkConfig;
using GatewayApi;
using Utils;

namespace GatewayClient
{
    public class GatewayClient
    {
        private readonly GatewayApiClient api;
        private readonly Mapper mapper = new Mapper();

        public GatewayClient(string baseUrl)
        {
            api = new GatewayApiClient(baseUrl, new HttpClient());
        }

        public GatewayClient(PrometheiNetwork network)
            : this(network.Team.Utils.Gateway)
        {
        }

        public LocalDataset GetManifest(string cid)
        {
            var response = Time.Wait(api.ManifestAsync(cid));
            return mapper.Map(response);
        }

        public Stream Download(string cid)
        {
            var response = Time.Wait(api.DataAsync(cid));
            return response.Stream;
        }
    }
}
