using Newtonsoft.Json.Linq;
using System.Numerics;
using Utils;

namespace PrometheiClient
{
    public class Mapper
    {
        public DebugInfo Map(PrometheiOpenApi.DebugInfo debugInfo)
        {
            return new DebugInfo
            {
                Id = debugInfo.Id,
                Spr = debugInfo.Spr,
                Addrs = debugInfo.Addrs.ToArray(),
                AnnounceAddresses = debugInfo.AnnounceAddresses.ToArray(),
                Version = Map(debugInfo.Promethei),
                Table = Map(debugInfo.Table)
            };
        }

        public DebugPeer Map(PrometheiOpenApi.DebugPeerRecord debugPeerRecord)
        {
            return new DebugPeer
            {
                PeerId = debugPeerRecord.PeerId,
                IsPeerFound = true,
                Addresses = debugPeerRecord.Addresses.ToArray()
            };
        }

        public LocalDatasetList Map(PrometheiOpenApi.DataList dataList)
        {
            return new LocalDatasetList
            {
                Content = dataList.Content.Select(Map).ToArray()
            };
        }

        public LocalDataset Map(PrometheiOpenApi.DataItem dataItem)
        {
            var manifest = MapManifest(dataItem.Manifest);
            return new LocalDataset
            {
                Cid = new ContentId(dataItem.Cid, manifest.DatasetSize),
                Manifest = manifest
            };
        }

        public DatasetStatus Map(PrometheiOpenApi.DatasetStatus datasetStatus, ContentId sourceCid)
        {
            return new DatasetStatus
            {
                Cid = new ContentId(datasetStatus.Cid, sourceCid.KnownFilesize),
                State = Map(datasetStatus.Status),
                ExpiryUtc = Time.ToUtcDateTime(datasetStatus.Expiry),
                Blocks = Map(datasetStatus.HasBlocks),
            };
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

        public PrometheiOpenApi.SalesAvailability Map(CreateStorageAvailability availability)
        {
            return new PrometheiOpenApi.SalesAvailability
            {
                MaximumDuration = ToLong(availability.MaxDuration.TotalSeconds),
                AvailableUntil = Convert.ToInt32(Time.ToUnixTimeSeconds(availability.UntilUtc)),
                MaximumCollateralPerByte = ToDecInt(availability.MaxCollateralPerByte),
                MinimumPricePerBytePerSecond = ToDecInt(availability.MinPricePerBytePerSecond)
            };
        }

        public PrometheiOpenApi.StorageRequestCreation Map(StoragePurchaseRequest purchase)
        {
            return new PrometheiOpenApi.StorageRequestCreation
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

        public StorageAvailability[] Map(ICollection<PrometheiOpenApi.SalesAvailability> availabilities)
        {
            return availabilities.Select(Map).ToArray();
        }

        public StorageAvailability Map(PrometheiOpenApi.SalesAvailability availability)
        {
            return new StorageAvailability
            (
                maxDuration: ToTimespan(availability.MaximumDuration),
                untilUtc: ToUtc(availability.AvailableUntil),
                minPricePerBytePerSecond: new TestToken(ToBigInt(availability.MinimumPricePerBytePerSecond)),
                maxCollateralPerByte: new TestToken(ToBigInt(availability.MaximumCollateralPerByte))
            );
        }

        public StorageSlotItem[] Map(ICollection<string> slotIds, Func<string, PrometheiOpenApi.SalesSlot> lookup)
        {
            return slotIds.Select(id => Map(lookup(id), id)).ToArray(); 
        }

        public StorageSlotItem Map(PrometheiOpenApi.SalesSlot slot, string slotId)
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

        public StoragePurchase Map(PrometheiOpenApi.Purchase purchase)
        {
            return new StoragePurchase
            {
                Request = Map(purchase.Request),
                State = Map(purchase.State),
                Error = purchase.Error
            };
        }

        public StoragePurchaseState Map(PrometheiOpenApi.PurchaseState purchaseState)
        {
            // Explicit mapping: If the API changes, we will get compile errors here.
            // That's what we want.
            switch (purchaseState)
            {
                case PrometheiOpenApi.PurchaseState.Cancelled:
                    return StoragePurchaseState.Cancelled;
                case PrometheiOpenApi.PurchaseState.Errored:
                    return StoragePurchaseState.Errored;
                case PrometheiOpenApi.PurchaseState.Failed:
                    return StoragePurchaseState.Failed;
                case PrometheiOpenApi.PurchaseState.Finished:
                    return StoragePurchaseState.Finished;
                case PrometheiOpenApi.PurchaseState.Pending:
                    return StoragePurchaseState.Pending;
                case PrometheiOpenApi.PurchaseState.Started:
                    return StoragePurchaseState.Started;
                case PrometheiOpenApi.PurchaseState.Submitted:
                    return StoragePurchaseState.Submitted;
                case PrometheiOpenApi.PurchaseState.Unknown:
                    return StoragePurchaseState.Unknown;
            }

            throw new Exception("API incompatibility detected. Unknown purchaseState: " + purchaseState.ToString());
        }

        public StorageSlotState Map(PrometheiOpenApi.SalesSlotState slotState)
        {
            // Explicit mapping: If the API changes, we will get compile errors here.
            // That's what we want.
            switch (slotState)
            {
                case PrometheiOpenApi.SalesSlotState.SaleCancelled:
                    return StorageSlotState.Cancelled;
                case PrometheiOpenApi.SalesSlotState.SaleDownloading:
                    return StorageSlotState.Downloading;
                case PrometheiOpenApi.SalesSlotState.SaleErrored:
                    return StorageSlotState.Errored;
                case PrometheiOpenApi.SalesSlotState.SaleFailed:
                    return StorageSlotState.Failed;
                case PrometheiOpenApi.SalesSlotState.SaleFilled:
                    return StorageSlotState.Filled;
                case PrometheiOpenApi.SalesSlotState.SaleFilling:
                    return StorageSlotState.Filling;
                case PrometheiOpenApi.SalesSlotState.SaleFinished:
                    return StorageSlotState.Finished;
                case PrometheiOpenApi.SalesSlotState.SaleIgnored:
                    return StorageSlotState.Ignored;
                case PrometheiOpenApi.SalesSlotState.SaleInitialProving:
                    return StorageSlotState.InitialProving;
                case PrometheiOpenApi.SalesSlotState.SalePayout:
                    return StorageSlotState.Payout;
                case PrometheiOpenApi.SalesSlotState.SalePreparing:
                    return StorageSlotState.Preparing;
                case PrometheiOpenApi.SalesSlotState.SaleProving:
                    return StorageSlotState.Proving;
                case PrometheiOpenApi.SalesSlotState.SaleUnknown:
                    return StorageSlotState.Unknown;
            }

            throw new Exception("API incompatibility detected. Unknown purchaseState: " + slotState.ToString());
        }

        public StorageRequest Map(PrometheiOpenApi.StorageRequest request)
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

        public StorageAsk Map(PrometheiOpenApi.StorageAsk ask)
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

        public StorageContent Map(PrometheiOpenApi.Content content)
        {
            return new StorageContent
            {
                Cid = content.Cid
            };
        }

        public PrometheiSpace Map(PrometheiOpenApi.Space space)
        {
            return new PrometheiSpace
            {
                QuotaMaxBytes = space.QuotaMaxBytes,
                QuotaReservedBytes = space.QuotaReservedBytes,
                QuotaUsedBytes = space.QuotaUsedBytes,
                TotalBlocks = space.TotalBlocks
            };
        }

        private DebugInfoVersion Map(PrometheiOpenApi.PrometheiVersion obj)
        {
            return new DebugInfoVersion
            {
                Version = obj.Version,
                Revision = obj.Revision,
                Contracts = obj.Contracts
            };
        }

        private DebugInfoTable Map(PrometheiOpenApi.PeersTable obj)
        {
            return new DebugInfoTable
            {
                LocalNode = Map(obj.LocalNode),
                Nodes = Map(obj.Nodes)
            };
        }

        private DebugInfoTableNode Map(PrometheiOpenApi.Node? token)
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

        private DebugInfoTableNode[] Map(ICollection<PrometheiOpenApi.Node> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return new DebugInfoTableNode[0];
            }

            return nodes.Select(Map).ToArray();
        }

        public DatasetStatusState Map(PrometheiOpenApi.DatasetStatusStatus status)
        {
            switch (status)
            {
                case PrometheiOpenApi.DatasetStatusStatus.Pending:
                    return DatasetStatusState.Pending;
                case PrometheiOpenApi.DatasetStatusStatus.Failure:
                    return DatasetStatusState.Failure;
                case PrometheiOpenApi.DatasetStatusStatus.Storing:
                    return DatasetStatusState.Storing;
                case PrometheiOpenApi.DatasetStatusStatus.Downloading:
                    return DatasetStatusState.Downloading;
                case PrometheiOpenApi.DatasetStatusStatus.Repairing:
                    return DatasetStatusState.Repairing;
                case PrometheiOpenApi.DatasetStatusStatus.Completed:
                    return DatasetStatusState.Completed;
                case PrometheiOpenApi.DatasetStatusStatus.Deleting:
                    return DatasetStatusState.Deleting;
                default:
                    throw new NotSupportedException();
            }
        }

        private Manifest MapManifest(PrometheiOpenApi.ManifestItem manifest)
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
