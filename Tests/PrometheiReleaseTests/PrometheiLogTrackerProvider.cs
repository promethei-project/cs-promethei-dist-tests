using PrometheiClient;
using PrometheiClient.Hooks;
using Utils;

namespace PrometheiTests
{
    public class PrometheiLogTrackerProvider  : IPrometheiHooksProvider
    {
        private readonly Action<IPrometheiNode> addNode;

        public PrometheiLogTrackerProvider(Action<IPrometheiNode> addNode)
        {
            this.addNode = addNode;
        }

        // See TestLifecycle.cs DownloadAllLogs()
        public IPrometheiNodeHooks CreateHooks(string nodeName)
        {
            return new PrometheiLogTracker(addNode);
        }

        public class PrometheiLogTracker : IPrometheiNodeHooks
        {
            private readonly Action<IPrometheiNode> addNode;

            public PrometheiLogTracker(Action<IPrometheiNode> addNode)
            {
                this.addNode = addNode;
            }

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
                addNode(node);
            }

            public void OnNodeRestarting()
            {
            }

            public void OnNodeRestarted()
            {
            }

            public void OnNodeStarting(DateTime startUtc, string image, EthAccount? ethAccount)
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
}
