using ArchivistOpenApi;
using Newtonsoft.Json;
using Utils;

namespace ArchivistClient
{
    public class DebugInfo
    {
        public string[] Addrs { get; set; } = Array.Empty<string>();
        public string Spr { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string[] AnnounceAddresses { get; set; } = Array.Empty<string>();
        public DebugInfoVersion Version { get; set; } = new();
        public DebugInfoTable Table { get; set; } = new();
    }

    public class DebugInfoVersion
    {
        public string Version { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public string Contracts { get; set; } = string.Empty;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Version) && !string.IsNullOrEmpty(Revision);
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class DebugInfoTable
    {
        public DebugInfoTableNode LocalNode { get; set; } = new();
        public DebugInfoTableNode[] Nodes { get; set; } = Array.Empty<DebugInfoTableNode>();
    }

    public class DebugInfoTableNode
    {
        public string NodeId { get; set; } = string.Empty;
        public string PeerId { get; set; } = string.Empty;
        public string Record { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public bool Seen { get; set; }
    }

    public class DebugPeer
    {
        public bool IsPeerFound { get; set; }
        public string PeerId { get; set; } = string.Empty;
        public string[] Addresses { get; set; } = Array.Empty<string>();
    }

    public class LocalDatasetList
    {
        public LocalDataset[] Content { get; set; } = Array.Empty<LocalDataset>();

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    } 

    public class LocalDataset
    {
        public ContentId Cid { get; set; } = new();
        public Manifest Manifest { get; set; } = new();
    }

    public class Manifest
    {
        public string RootHash { get; set; } = string.Empty;
        public ByteSize DatasetSize { get; set; } = ByteSize.Zero;
        public ByteSize BlockSize { get; set; } = ByteSize.Zero;
        public bool Protected { get; set; }
        public string Filename { get; set; } = string.Empty;
        public string Mimetype { get; set; } = string.Empty;

        public int NumBlocks => DatasetSize.DivUp(BlockSize);
    }

    public class SalesRequestStorageRequest
    {
        public string Duration { get; set; } = string.Empty;
        public string ProofProbability { get; set; } = string.Empty;
        public string Reward { get; set; } = string.Empty;
        public string Collateral { get; set; } = string.Empty;
        public string? Expiry { get; set; }
        public uint? Nodes { get; set; }
        public uint? Tolerance { get; set; }
    }

    public class ContentId
    {
        public ContentId()
        {
            Id = string.Empty;
            KnownFilesize = 0.Bytes();
        }

        public ContentId(string id)
        {
            Id = id;
            KnownFilesize = 0.Bytes();
        }

        public ContentId(string id, ByteSize knownFilesize)
        {
            Id = id;
            KnownFilesize = knownFilesize;
        }

        public string Id { get; }
        public ByteSize KnownFilesize { get; }

        public override string ToString()
        {
            return Id;
        }

        public override bool Equals(object? obj)
        {
            return obj is ContentId id && Id == id.Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        public static bool operator ==(ContentId a, ContentId b)
        {
            return a.Id == b.Id;
        }

        public static bool operator !=(ContentId a, ContentId b)
        {
            return a.Id != b.Id;
        }
    }

    public class ArchivistSpace
    {
        public long TotalBlocks { get; set; }
        public long QuotaMaxBytes { get; set; }
        public long QuotaUsedBytes { get; set; }
        public long QuotaReservedBytes { get; set; }
        public long FreeBytes => QuotaMaxBytes - (QuotaUsedBytes + QuotaReservedBytes);

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class DatasetStatusSlot
    {
        public ContentId Cid { get; set; } = new ContentId();
        public DatasetStatusState State { get; set; }
        public DateTime ExpiryUtc { get; set; }

        public override string ToString()
        {
            return $"(SlotCid:{Cid} State:{State} ExpiryUtc:{Time.FormatTimestamp(ExpiryUtc)})";
        }
    }

    public class DatasetStatus
    {
        public ContentId Cid { get; set; } = new ContentId();
        public DatasetStatusState State { get; set; }
        public DateTime ExpiryUtc { get; set; }
        public IndexSet Blocks { get; set; } = new IndexSet();
        public DatasetStatusSlot[] Slots { get; set; } = Array.Empty<DatasetStatusSlot>();

        public override string ToString()
        {
            return $"(Cid:{Cid} State:{State} ExpiryUtc:{Time.FormatTimestamp(ExpiryUtc)} Blocks:[{Blocks}] Slots: [{
                string.Join(",", Slots.Select(s => s.ToString()))
            }])";
        }
    }

    public enum DatasetStatusState
    {
        Pending,
        Failure,
        Storing,
        Downloading,
        Repairing,
        Completed,
        Deleting,
    }
}
