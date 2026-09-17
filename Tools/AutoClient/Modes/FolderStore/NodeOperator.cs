using PrometheiClient;
using Logging;

namespace AutoClient.Modes.FolderStore
{
    public interface INodeOperations
    {
        void CreateNewPurchase(string filePath);
        void ExtendPurchase(FileStatus fileStatus);
    }

    public class NodeOperator : INodeOperations
    {
        private readonly ILog log;
        private readonly FolderStatus folderStatus;
        private readonly NodeDispatcher dispatcher;
        private readonly IAppEventHandler appEventHandler;

        public NodeOperator(ILog log, FolderStatus folderStatus, NodeDispatcher dispatcher, IAppEventHandler appEventHandler)
        {
            this.log = new LogPrefixer(log, "(Node)");
            this.folderStatus = folderStatus;
            this.dispatcher = dispatcher;
            this.appEventHandler = appEventHandler;
        }

        public void CreateNewPurchase(string filePath)
        {
            OnNodeAction(filePath, a => a.CreateNewPurchase(filePath));
        }

        public void ExtendPurchase(FileStatus fileStatus)
        {
            OnNodeAction(fileStatus, a => a.ExtendPurchase());
        }

        private void OnNodeAction(string filePath, Action<NodeAction> action)
        {
            var localFilename = Path.GetFileName(filePath);
            var entry = folderStatus.GetEntry(localFilename);

            OnNodeAction(entry, action);
        }

        private void OnNodeAction(FileStatus entry, Action<NodeAction> action)
        {
            var prefix = $"['{entry.Filename}']";
            dispatcher.OnNode(prefix, node =>
            {
                var nodeAction = new NodeAction(log, node, entry, appEventHandler);
                action(nodeAction);
            },
            whenDone: folderStatus.SaveChanges);
        }

        public class NodeAction
        {
            private readonly ILog log;
            private readonly PrometheiWrapper node;
            private readonly FileStatus entry;
            private readonly IAppEventHandler appEventHandler;

            public NodeAction(ILog log, PrometheiWrapper node, FileStatus entry, IAppEventHandler appEventHandler)
            {
                this.log = log;
                this.node = node;
                this.entry = entry;
                this.appEventHandler = appEventHandler;
            }

            public void CreateNewPurchase(string filePath)
            {
                PerformQuotaCheck(filePath);
                var cid = UploadFile(filePath);
                CreatePurchase(cid);
            }

            public void ExtendPurchase()
            {
                Log("Extending existing purchase...");
                try
                {
                    var request = node.ExtendStorage(
                        new ContentId(entry.EncodedCid),
                        entry.PurchaseNodes,
                        entry.PurchaseTolerance
                    );
                    WaitForStartOfRequest(request);
                    appEventHandler.OnPurchaseExtendSuccess();
                }
                catch (Exception exc)
                {
                    log.Error("Failed to extend purchase: " + exc);
                    appEventHandler.OnPurchaseExtendFailure();
                    throw;
                }
            }

            private void CreatePurchase(string cid)
            {
                Log("Creating new purchase...");
                try
                {
                    var request = node.RequestStorage(new ContentId(cid));
                    WaitForStartOfRequest(request);
                    appEventHandler.OnPurchaseNewSuccess();
                }
                catch (Exception exc)
                {
                    log.Error("Failed to start new purchase: " + exc);
                    appEventHandler.OnPurchaseNewFailure();
                    throw;
                }
            }

            private void PerformQuotaCheck(string filePath)
            {
                var quotaCheck = new QuotaCheck(log, filePath, node);
                if (!quotaCheck.IsLocalQuotaAvailable())
                {
                    log.Error("Quota check failed: Node does not have enough space to upload this file at this time.");
                    throw new InvalidOperationException("Quota check failed");
                }
            }

            private string UploadFile(string filePath)
            {
                Log("Uploading file...");
                try
                {
                    var cid = node.UploadFile(filePath).Id;
                    appEventHandler.OnUploadSuccess();
                    Log($"Successfully uploaded. BasicCid: '{cid}'");
                    return cid;
                }
                catch (Exception exc)
                {
                    appEventHandler.OnUploadFailure();
                    log.Error("Failed to upload: " + exc);
                    throw;
                }
            }

            private void WaitForStartOfRequest(IStoragePurchaseContract request)
            {
                request.WaitForStorageContractSubmitted();
                request.WaitForStorageContractStarted();

                entry.EncodedCid = request.EncodedContentId.Id;
                entry.PurchaseNodes = request.Purchase.PurchaseParams.Nodes;
                entry.PurchaseTolerance = request.Purchase.PurchaseParams.Tolerance;
                entry.PurchaseFinishedUtc = DateTime.UtcNow + request.Purchase.PurchaseParams.Duration;

                Log($"Successfully started new purchase: '{request.PurchaseId}'");
            }

            private void Log(string v)
            {
                log.Log(v);
            }
        }
    }
}
