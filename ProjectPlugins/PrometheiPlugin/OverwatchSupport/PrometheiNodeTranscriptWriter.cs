using PrometheiClient;
using PrometheiClient.Hooks;
using OverwatchTranscript;
using Utils;

namespace PrometheiPlugin.OverwatchSupport
{
    public class PrometheiNodeTranscriptWriter : IPrometheiNodeHooks
    {
        private readonly ITranscriptWriter writer;
        private readonly IdentityMap identityMap;
        private readonly string name;
        private int identityIndex = -1;
        private readonly List<(DateTime, OverwatchPrometheiEvent)> pendingEvents = new List<(DateTime, OverwatchPrometheiEvent)>();

        public PrometheiNodeTranscriptWriter(ITranscriptWriter writer, IdentityMap identityMap, string name)
        {
            this.writer = writer;
            this.identityMap = identityMap;
            this.name = name;
        }

        public void OnNodeStarting(DateTime startUtc, string image, EthAccount? ethAccount)
        {
            WritePrometheiEvent(startUtc, e =>
            {
                e.NodeStarting = new NodeStartingEvent
                {
                    Image = image,
                    EthAddress = ethAccount != null ? ethAccount.ToString() : ""
                };
            });
        }

        public void OnNodeStarted(IPrometheiNode node, string peerId, string nodeId)
        {
            if (string.IsNullOrEmpty(peerId) || string.IsNullOrEmpty(nodeId))
            {
                throw new Exception("Node started - peerId and/or nodeId unknown.");
            }

            identityMap.Add(name, peerId, nodeId);
            identityIndex = identityMap.GetIndex(name);

            WritePrometheiEvent(e =>
            {
                e.NodeStarted = new NodeStartedEvent
                {
                };
            });
        }

        public void OnNodeStopping()
        {
            WritePrometheiEvent(e =>
            {
                e.NodeStopping = new NodeStoppingEvent
                {
                };
            });
        }

        public void OnNodeRestarting()
        {
            throw new NotImplementedException();
        }

        public void OnNodeRestarted()
        {
            throw new NotImplementedException();
        }

        public void OnFileDownloading(ContentId cid)
        {
            WritePrometheiEvent(e =>
            {
                e.FileDownloading = new FileDownloadingEvent
                {
                    Cid = cid.Id
                };
            });
        }

        public void OnFileDownloaded(ByteSize size, ContentId cid)
        {
            WritePrometheiEvent(e =>
            {
                e.FileDownloaded = new FileDownloadedEvent
                {
                    Cid = cid.Id,
                    ByteSize = size.SizeInBytes
                };
            });
        }

        public void OnFileUploading(string uid, ByteSize size)
        {
            WritePrometheiEvent(e =>
            {
                e.FileUploading = new FileUploadingEvent
                {
                    UniqueId = uid,
                    ByteSize = size.SizeInBytes
                };
            });
        }

        public void OnFileUploaded(string uid, ByteSize size, ContentId cid)
        {
            WritePrometheiEvent(e =>
            {
                e.FileUploaded = new FileUploadedEvent
                { 
                    UniqueId = uid,
                    Cid = cid.Id,
                    ByteSize = size.SizeInBytes
                };
            });
        }

        public void OnStorageContractSubmitted(StoragePurchaseContract storagePurchaseContract)
        {
            WritePrometheiEvent(e =>
            {
                e.StorageContractSubmitted = new StorageContractSubmittedEvent
                {
                    PurchaseId = storagePurchaseContract.PurchaseId,
                    PurchaseRequest = storagePurchaseContract.Purchase
                };
            });
        }

        public void OnStorageContractUpdated(StoragePurchase purchaseStatus)
        {
            WritePrometheiEvent(e =>
            {
                e.StorageContractUpdated = new StorageContractUpdatedEvent
                {
                    StoragePurchase = purchaseStatus
                };
            });
        }

        public void OnStorageAvailabilityCreated()
        {
            WritePrometheiEvent(e =>
            {
                e.StorageAvailabilityCreated = new StorageAvailabilityCreatedEvent
                {
                };
            });
        }

        private void WritePrometheiEvent(Action<OverwatchPrometheiEvent> action)
        {
            WritePrometheiEvent(DateTime.UtcNow, action);
        }

        private void WritePrometheiEvent(DateTime utc, Action<OverwatchPrometheiEvent> action)
        {
            var e = new OverwatchPrometheiEvent
            {
                NodeIdentity = identityIndex
            };

            action(e);

            if (identityIndex < 0)
            {
                // If we don't know our id, don't write the events yet.
                AddToCache(utc, e);
            }
            else
            {
                e.Write(utc, writer);

                // Write any events that we cached when we didn't have our id yet.
                WriteAndClearCache();
            }
        }

        private void AddToCache(DateTime utc, OverwatchPrometheiEvent e)
        {
            pendingEvents.Add((utc, e));
        }

        private void WriteAndClearCache()
        {
            if (pendingEvents.Any())
            {
                foreach (var pair in pendingEvents)
                {
                    pair.Item2.NodeIdentity = identityIndex;
                    pair.Item2.Write(pair.Item1, writer);
                }
                pendingEvents.Clear();
            }
        }
    }
}
