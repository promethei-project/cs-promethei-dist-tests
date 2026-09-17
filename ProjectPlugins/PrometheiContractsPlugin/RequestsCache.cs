using PrometheiContractsPlugin.Marketplace;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;

namespace PrometheiContractsPlugin
{
    public interface IRequestsCache
    {
        void Add(byte[] requestId, CacheRequest request);
        CacheRequest? Get(byte[] requestId);
        void Delete(byte[] requestId);
        void IterateAll(Action<byte[]> onRequestId);
    }

    public class CacheRequest
    {
        public CacheRequest(Request request, DateTime expiryUtc, DateTime finishUtc)
        {
            Request = request;
            ExpiryUtc = expiryUtc;
            FinishUtc = finishUtc;
        }

        public Request Request { get; }
        public DateTime ExpiryUtc { get; }
        public DateTime FinishUtc { get; }
    }

    public class NullRequestsCache : IRequestsCache
    {
        public void Add(byte[] requestId, CacheRequest request)
        {
        }

        public CacheRequest? Get(byte[] requestId)
        {
            return null;
        }

        public void Delete(byte[] requestId)
        {
        }

        public void IterateAll(Action<byte[]> onRequestId)
        {
        }
    }

    public class DiskRequestsCache : IRequestsCache
    {
        private readonly string dataDir;

        public DiskRequestsCache(string dataDir)
        {
            this.dataDir = dataDir;
            Directory.CreateDirectory(dataDir);
        }

        public void Add(byte[] requestId, CacheRequest request)
        {
            var filename = FilePath(requestId);
            if (File.Exists(filename)) return;
            File.WriteAllText(filename, JsonConvert.SerializeObject(request));
        }

        public CacheRequest? Get(byte[] requestId)
        {
            var filename = FilePath(requestId);
            if (!File.Exists(filename)) return null;

            try
            {
                var text = File.ReadAllText(filename);
                var result = JsonConvert.DeserializeObject<CacheRequest>(text);
                if (result == null ||
                    result.Request == null ||
                    result.Request.Expiry < 1 ||
                    result.Request.Ask == null ||
                    result.Request.Ask.Duration < 1)
                {
                    File.Delete(filename);
                    return null;
                }
                return result;
            }
            catch
            {
                File.Delete(filename);
                return null;
            }
        }

        public void Delete(byte[] requestId)
        {
            var filename = FilePath(requestId);
            if (File.Exists(filename)) File.Delete(filename);
        }

        public void IterateAll(Action<byte[]> onRequestId)
        {
            var files = Directory.GetFiles(dataDir);
            foreach (var file in files)
            {
                if (file.EndsWith(".json"))
                {
                    var name = Path.GetFileName(file);
                    try
                    {
                        var requestId = name.Substring(0, name.Length - 5).HexToByteArray();
                        onRequestId(requestId);
                    }
                    catch
                    {
                        // Unknown json file in datadir?
                    }
                }
            }
        }

        private string FilePath(byte[] id)
        {
            return Path.Combine(dataDir, id.ToHex().ToLowerInvariant() + ".json");
        }
    }
}
