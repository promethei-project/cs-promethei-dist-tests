using Utils;

namespace PrometheiClient.Hooks
{
    public interface IPrometheiHooksProvider
    {
        IPrometheiNodeHooks CreateHooks(string nodeName);
    }

    public class PrometheiHooksFactory
    {
        public List<IPrometheiHooksProvider> Providers { get; } = new List<IPrometheiHooksProvider>();

        public IPrometheiNodeHooks CreateHooks(string nodeName)
        {
            if (Providers.Count == 0) return new DoNothingPrometheiHooks();

            var hooks = Providers.Select(p => p.CreateHooks(nodeName)).ToArray();
            return new MuxingPrometheiNodeHooks(hooks);
        }
    }

    public class DoNothingHooksProvider : IPrometheiHooksProvider
    {
        public IPrometheiNodeHooks CreateHooks(string nodeName)
        {
            return new DoNothingPrometheiHooks();
        }
    }

    public class DoNothingPrometheiHooks : IPrometheiNodeHooks
    {
        public void OnFileDownloaded(ByteSize size, ContentId cid)
        {
        }

        public void OnFileDownloading(ContentId cid)
        {
        }

        public void OnFileUploaded(string uid, ByteSize size, ContentId cid)
        {
        }

        public void OnFileUploading(string uid, ByteSize size)
        {
        }

        public void OnNodeStarted(IPrometheiNode node, string peerId, string nodeId)
        {
        }

        public void OnNodeStarting(DateTime startUtc, string image, EthAccount? ethAccount)
        {
        }

        public void OnNodeRestarting()
        {
        }

        public void OnNodeRestarted()
        {
        }

        public void OnNodeStopping()
        {
        }

        public void OnStorageAvailabilityCreated()
        {
        }

        public void OnStorageContractSubmitted(StoragePurchaseContract storagePurchaseContract)
        {
        }

        public void OnStorageContractUpdated(StoragePurchase purchaseStatus)
        {
        }
    }
}
