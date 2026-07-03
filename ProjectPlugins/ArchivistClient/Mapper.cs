using Newtonsoft.Json.Linq;
using System.Numerics;
using Utils;

namespace ArchivistClient
{
    public class Mapper
    {
        public DebugInfo Map(ArchivistOpenApi.DebugInfo debugInfo)
        {
            return new DebugInfo
            {
                Id = debugInfo.Id,
                Spr = debugInfo.Spr,
                Addrs = debugInfo.Addrs.ToArray(),
                AnnounceAddresses = debugInfo.AnnounceAddresses.ToArray(),
                Version = Map(debugInfo.Archivist),
                Table = Map(debugInfo.Table)
            };
        }

        public DebugPeer Map(ArchivistOpenApi.DebugPeerRecord debugPeerRecord)
        {
            return new DebugPeer
            {
                PeerId = debugPeerRecord.PeerId,
                IsPeerFound = true,
                Addresses = debugPeerRecord.Addresses.ToArray()
            };
        }

        public LocalDatasetList Map(ArchivistOpenApi.DataList dataList)
        {
            return new LocalDatasetList
            {
                Content = dataList.Content.Select(Map).ToArray()
            };
        }

        public LocalDataset Map(ArchivistOpenApi.DataItem dataItem)
        {
            var manifest = MapManifest(dataItem.Manifest);
            return new LocalDataset
            {
                Cid = new ContentId(dataItem.Cid, manifest.DatasetSize),
                Manifest = manifest
            };
        }

        public DatasetStatus Map(ArchivistOpenApi.DatasetStatus datasetStatus, ContentId sourceCid)
        {
            return new DatasetStatus
            {
                Cid = new ContentId(datasetStatus.Cid, sourceCid.KnownFilesize),
                State = Map(datasetStatus.Status),
                ExpiryUtc = Time.ToUtcDateTime(datasetStatus.Expiry),
                Blocks = Map(datasetStatus.HasBlocks),
                Slots = Map(datasetStatus.Slots)
            };
        }

        private DatasetStatusSlot[] Map(ICollection<ArchivistOpenApi.Slots> slots)
        {
            return slots.Select(s => new DatasetStatusSlot
            {
                Cid = new ContentId(s.Cid),
                State = Map(s.Status),
                ExpiryUtc = Time.ToUtcDateTime(s.Expiry),                
            }).ToArray();
        }

        private IndexSet Map(IEnumerable<bool> bitmap)
        {
            var result = new IndexSet();
            var index = 0;
            foreach (var b in bitmap)
            {
                result[index] = b;
                index++;
            }

            return result;
        }

        public ArchivistOpenApi.SalesAvailability Map(CreateStorageAvailability availability)
        {
            return new ArchivistOpenApi.SalesAvailability
            {
                MaximumDuration = ToLong(availability.MaxDuration.TotalSeconds),
                AvailableUntil = Convert.ToInt32(Time.ToUnixTimeSeconds(availability.UntilUtc)),
                MaximumCollateralPerByte = ToDecInt(availability.MaxCollateralPerByte),
                MinimumPricePerBytePerSecond = ToDecInt(availability.MinPricePerBytePerSecond)
            };
        }

        public ArchivistOpenApi.StorageRequestCreation Map(StoragePurchaseRequest purchase)
        {
            return new ArchivistOpenApi.StorageRequestCreation
            {
                Duration = ToLong(purchase.PurchaseParams.Duration.TotalSeconds),
                ProofProbability = ToDecInt(purchase.PurchaseParams.ProofProbability),
                PricePerBytePerSecond = ToDecInt(purchase.PurchaseParams.PricePerByteSecond),
                CollateralPerByte = ToDecInt(purchase.PurchaseParams.CollateralPerByte),
                Expiry = ToLong(purchase.PurchaseParams.Expiry.TotalSeconds),
                Nodes = Convert.ToInt32(purchase.PurchaseParams.Nodes),
                Tolerance = Convert.ToInt32(purchase.PurchaseParams.Tolerance)
            };
        }

        public StorageAvailability[] Map(ICollection<ArchivistOpenApi.SalesAvailability> availabilities)
        {
            return availabilities.Select(Map).ToArray();
        }

        public StorageAvailability Map(ArchivistOpenApi.SalesAvailability availability)
        {
            return new StorageAvailability
            (
                maxDuration: ToTimespan(availability.MaximumDuration),
                untilUtc: ToUtc(availability.AvailableUntil),
                minPricePerBytePerSecond: new TestToken(ToBigInt(availability.MinimumPricePerBytePerSecond)),
                maxCollateralPerByte: new TestToken(ToBigInt(availability.MaximumCollateralPerByte))
            );
        }

        public StorageSlotItem[] Map(ICollection<string> slotIds, Func<string, ArchivistOpenApi.SalesSlot> lookup)
        {
            return slotIds.Select(id => Map(lookup(id), id)).ToArray(); 
        }

        public StorageSlotItem Map(ArchivistOpenApi.SalesSlot slot, string slotId)
        {
            return new StorageSlotItem
            {
                SlotId = slotId,
                SlotIndex = slot.SlotIndex,
                RequestId = slot.RequestId,
                Request = Map(slot.Request),
                State = Map(slot.State)
            };
        }

        public StoragePurchase Map(ArchivistOpenApi.Purchase purchase)
        {
            return new StoragePurchase
            {
                Request = Map(purchase.Request),
                State = Map(purchase.State),
                Error = purchase.Error
            };
        }

        public StoragePurchaseState Map(ArchivistOpenApi.PurchaseState purchaseState)
        {
            // Explicit mapping: If the API changes, we will get compile errors here.
            // That's what we want.
            switch (purchaseState)
            {
                case ArchivistOpenApi.PurchaseState.Cancelled:
                    return StoragePurchaseState.Cancelled;
                case ArchivistOpenApi.PurchaseState.Errored:
                    return StoragePurchaseState.Errored;
                case ArchivistOpenApi.PurchaseState.Failed:
                    return StoragePurchaseState.Failed;
                case ArchivistOpenApi.PurchaseState.Finished:
                    return StoragePurchaseState.Finished;
                case ArchivistOpenApi.PurchaseState.Pending:
                    return StoragePurchaseState.Pending;
                case ArchivistOpenApi.PurchaseState.Started:
                    return StoragePurchaseState.Started;
                case ArchivistOpenApi.PurchaseState.Submitted:
                    return StoragePurchaseState.Submitted;
                case ArchivistOpenApi.PurchaseState.Unknown:
                    return StoragePurchaseState.Unknown;
            }

            throw new Exception("API incompatibility detected. Unknown purchaseState: " + purchaseState.ToString());
        }

        public StorageSlotState Map(ArchivistOpenApi.SalesSlotState slotState)
        {
            // Explicit mapping: If the API changes, we will get compile errors here.
            // That's what we want.
            switch (slotState)
            {
                case ArchivistOpenApi.SalesSlotState.SaleCancelled:
                    return StorageSlotState.Cancelled;
                case ArchivistOpenApi.SalesSlotState.SaleDownloading:
                    return StorageSlotState.Downloading;
                case ArchivistOpenApi.SalesSlotState.SaleErrored:
                    return StorageSlotState.Errored;
                case ArchivistOpenApi.SalesSlotState.SaleFailed:
                    return StorageSlotState.Failed;
                case ArchivistOpenApi.SalesSlotState.SaleFilled:
                    return StorageSlotState.Filled;
                case ArchivistOpenApi.SalesSlotState.SaleFilling:
                    return StorageSlotState.Filling;
                case ArchivistOpenApi.SalesSlotState.SaleFinished:
                    return StorageSlotState.Finished;
                case ArchivistOpenApi.SalesSlotState.SaleIgnored:
                    return StorageSlotState.Ignored;
                case ArchivistOpenApi.SalesSlotState.SaleInitialProving:
                    return StorageSlotState.InitialProving;
                case ArchivistOpenApi.SalesSlotState.SalePayout:
                    return StorageSlotState.Payout;
                case ArchivistOpenApi.SalesSlotState.SalePreparing:
                    return StorageSlotState.Preparing;
                case ArchivistOpenApi.SalesSlotState.SaleProving:
                    return StorageSlotState.Proving;
                case ArchivistOpenApi.SalesSlotState.SaleUnknown:
                    return StorageSlotState.Unknown;
            }

            throw new Exception("API incompatibility detected. Unknown purchaseState: " + slotState.ToString());
        }

        public StorageRequest Map(ArchivistOpenApi.StorageRequest request)
        {
            return new StorageRequest
            {
                Ask = Map(request.Ask),
                Content = Map(request.Content),
                Id = request.Id,
                Client = request.Client,
                Expiry = request.Expiry,
                Nonce = request.Nonce
            };
        }

        public StorageAsk Map(ArchivistOpenApi.StorageAsk ask)
        {
            return new StorageAsk
            {
                Duration = ask.Duration,
                MaxSlotLoss = ask.MaxSlotLoss,
                ProofProbability = ask.ProofProbability,
                PricePerBytePerSecond = ask.PricePerBytePerSecond,
                Slots = ask.Slots,
                SlotSize = ask.SlotSize
            };
        }

        public StorageContent Map(ArchivistOpenApi.Content content)
        {
            return new StorageContent
            {
                Cid = content.Cid
            };
        }

        public ArchivistSpace Map(ArchivistOpenApi.Space space)
        {
            return new ArchivistSpace
            {
                QuotaMaxBytes = space.QuotaMaxBytes,
                QuotaReservedBytes = space.QuotaReservedBytes,
                QuotaUsedBytes = space.QuotaUsedBytes,
                TotalBlocks = space.TotalBlocks
            };
        }

        private DebugInfoVersion Map(ArchivistOpenApi.ArchivistVersion obj)
        {
            return new DebugInfoVersion
            {
                Version = obj.Version,
                Revision = obj.Revision,
                Contracts = obj.Contracts
            };
        }

        private DebugInfoTable Map(ArchivistOpenApi.PeersTable obj)
        {
            return new DebugInfoTable
            {
                LocalNode = Map(obj.LocalNode),
                Nodes = Map(obj.Nodes)
            };
        }

        private DebugInfoTableNode Map(ArchivistOpenApi.Node? token)
        {
            if (token == null) return new DebugInfoTableNode();
            return new DebugInfoTableNode
            {
                Address = token.Address,
                NodeId = token.NodeId,
                PeerId = token.PeerId,
                Record = token.Record,
                Seen = token.Seen
            };
        }

        private DebugInfoTableNode[] Map(ICollection<ArchivistOpenApi.Node> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return new DebugInfoTableNode[0];
            }

            return nodes.Select(Map).ToArray();
        }

        public DatasetStatusState Map(ArchivistOpenApi.DatasetStatusStatus status)
        {
            switch (status)
            {
                case ArchivistOpenApi.DatasetStatusStatus.Pending:
                    return DatasetStatusState.Pending;
                case ArchivistOpenApi.DatasetStatusStatus.Failure:
                    return DatasetStatusState.Failure;
                case ArchivistOpenApi.DatasetStatusStatus.Storing:
                    return DatasetStatusState.Storing;
                case ArchivistOpenApi.DatasetStatusStatus.Downloading:
                    return DatasetStatusState.Downloading;
                case ArchivistOpenApi.DatasetStatusStatus.Repairing:
                    return DatasetStatusState.Repairing;
                case ArchivistOpenApi.DatasetStatusStatus.Completed:
                    return DatasetStatusState.Completed;
                case ArchivistOpenApi.DatasetStatusStatus.Deleting:
                    return DatasetStatusState.Deleting;
                default:
                    throw new NotSupportedException();
            }
        }

        private DatasetStatusState Map(ArchivistOpenApi.SlotsStatus status)
        {
            switch (status)
            {
                case ArchivistOpenApi.SlotsStatus.Pending:
                    return DatasetStatusState.Pending;
                case ArchivistOpenApi.SlotsStatus.Failure:
                    return DatasetStatusState.Failure;
                case ArchivistOpenApi.SlotsStatus.Storing:
                    return DatasetStatusState.Storing;
                case ArchivistOpenApi.SlotsStatus.Downloading:
                    return DatasetStatusState.Downloading;
                case ArchivistOpenApi.SlotsStatus.Repairing:
                    return DatasetStatusState.Repairing;
                case ArchivistOpenApi.SlotsStatus.Completed:
                    return DatasetStatusState.Completed;
                case ArchivistOpenApi.SlotsStatus.Deleting:
                    return DatasetStatusState.Deleting;
                default:
                    throw new NotSupportedException();
            }
        }

        private Manifest MapManifest(ArchivistOpenApi.ManifestItem manifest)
        {
            return new Manifest
            {
                BlockSize = new ByteSize(Convert.ToInt64(manifest.BlockSize)),
                DatasetSize = new ByteSize(Convert.ToInt64(manifest.DatasetSize)),
                RootHash = manifest.TreeCid,
                Protected = manifest.Protected,
                Filename = manifest.Filename,
                Mimetype = manifest.Mimetype
            };
        }

        private JArray JArray(IDictionary<string, object> map, string name)
        {
            return (JArray)map[name];
        }

        private JObject JObject(IDictionary<string, object> map, string name)
        {
            return (JObject)map[name];
        }

        private string StringOrEmpty(JObject obj, string name)
        {
            if (obj.TryGetValue(name, out var token))
            {
                var str = (string?)token;
                if (!string.IsNullOrEmpty(str)) return str;
            }
            return string.Empty;
        }

        private bool Bool(JObject obj, string name)
        {
            if (obj.TryGetValue(name, out var token))
            {
                return (bool)token;
            }
            return false;
        }

        private string ToDecInt(double d)
        {
            var i = new BigInteger(d);
            return i.ToString("D");
        }

        private string ToDecInt(TestToken t)
        {
            return t.TstWei.ToString("D");
        }

        private TestToken ToTestToken(string s)
        {
            return new TestToken(ToBigInt(s));
        }

        private long ToLong(double value)
        {
            return Convert.ToInt64(value);
        }

        private BigInteger ToBigInt(string tokens)
        {
            return BigInteger.Parse(tokens);
        }

        private TimeSpan ToTimespan(long duration)
        {
            return TimeSpan.FromSeconds(duration);
        }

        private DateTime ToUtc(int utc)
        {
            return Time.ToUtcDateTime(utc);
        }

        private ByteSize ToByteSize(long size)
        {
            return new ByteSize(size);
        }
    }
}
