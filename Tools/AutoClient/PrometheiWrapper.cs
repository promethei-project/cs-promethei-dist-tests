using PrometheiClient;
using FileUtils;
using Logging;
using Utils;

namespace AutoClient
{
    public class PrometheiWrapper
    {
        private readonly App app;
        private readonly LogPrefixer modifyingLogPrefixer;
        private readonly IPrometheiNode node;

        private static readonly Random r = new Random();

        public PrometheiWrapper(App app, IPrometheiNode node, LogPrefixer modifyingLogPrefixer)
        {
            this.app = app;
            this.modifyingLogPrefixer = modifyingLogPrefixer;
            this.node = node;
        }

        public void SetLogPrefix(string prefix)
        {
            modifyingLogPrefixer.Prefix = prefix;
        }

        public PrometheiSpace Space()
        {
            return node.Space();
        }

        public ContentId UploadFile(string filepath)
        {
            return node.UploadFile(TrackedFile.FromPath(app.Log, filepath));
        }

        public StoragePurchase? GetStoragePurchase(string pid)
        {
            var purchases = node.GetPurchases();
            if (!purchases.Contains(pid.ToLowerInvariant())) return null;
            return node.GetPurchaseStatus(pid);
        }

        public IStoragePurchaseContract RequestStorage(ContentId cid)
        {
            var durability = GetDurabilityValues();
            var result = node.Marketplace.RequestStorage(new StoragePurchaseRequest(cid, p => p
                .WithCollateralPerByte(app.Config.CollateralPerByte.TstWei())
                .WithDuration(GetDuration())
                .WithExpiry(TimeSpan.FromMinutes(app.Config.ContractExpiryMinutes))
                .WithNodes(durability.Nodes)
                .WithTolerance(durability.Tolerance)
                .WithPricePerByteSecond(GetPricePerBytePerSecond())
                .WithProofProbability(durability.ProofProbability)
            ));
            return result;
        }

        public IStoragePurchaseContract ExtendStorage(ContentId cid, int nodes, int tolerance)
        {
            var durability = GetDurabilityValues();
            var result = node.Marketplace.RequestStorage(new StoragePurchaseRequest(cid, p => p
                .WithCollateralPerByte(app.Config.CollateralPerByte.TstWei())
                .WithDuration(GetDuration())
                .WithExpiry(TimeSpan.FromMinutes(app.Config.ContractExpiryMinutes))
                .WithNodes(nodes)
                .WithTolerance(tolerance)
                .WithPricePerByteSecond(GetPricePerBytePerSecond())
                .WithProofProbability(durability.ProofProbability)
            ));
            return result;
        }

        private DurabilityValues GetDurabilityValues()
        {
            return RandomUtils.GetOneRandom(app.Config.DurabilityValues);
        }

        private TestToken GetPricePerBytePerSecond()
        {
            var i = app.Config.PricePerBytePerSecond;
            i -= 100;
            i += r.Next(0, 1000);

            return i.TstWei();
        }

        private TimeSpan GetDuration()
        {
            var durations = app.Config.Durations;
            if (durations.Length == 1)
            {
                return durations[0];
            }
            if (durations.Length == 2)
            {
                var seconds = r.Next(Convert.ToInt32(durations[0].TotalSeconds), Convert.ToInt32(durations[1].TotalSeconds));
                return TimeSpan.FromSeconds(seconds);
            }
            throw new Exception("Misconfigured DurationMinutes");
        }
    }
}
