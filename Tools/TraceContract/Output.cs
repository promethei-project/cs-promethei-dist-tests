using System.Numerics;
using BlockchainUtils;
using PrometheiContractsPlugin.ChainMonitor;
using PrometheiContractsPlugin.Marketplace;
using Logging;
using Newtonsoft.Json;
using Utils;
using Nethereum.Hex.HexConvertors.Extensions;
using PrometheiNetworkConfig;

namespace TraceContract
{
    public class Output
    {
        private class Entry
        {
            public Entry(BlockTimeEntry blk, string msg)
            {
                Utc = blk.Utc;
                Msg = msg;
            }

            public Entry(DateTime utc, string msg)
            {
                Utc = utc;
                Msg = msg;
            }

            public DateTime Utc{ get; }
            public string Msg { get; }
        }

        private readonly ILog log;
        private readonly List<SlotTrackerChainStateChangeHandler> slotTrackers = new List<SlotTrackerChainStateChangeHandler>();
        private readonly List<Entry> entries = new();
        private readonly string folder;

        public Output(ILog log, Input input, Config config, PrometheiNetwork network)
        {
            folder = config.OuputFolder;
            Directory.CreateDirectory(folder);

            var filename = Path.Combine(folder, $"contract_{input.PurchaseId}");
            var fileLog = new FileLog(filename);
            log.Log($"Logging to '{filename}'");

            this.log = new LogSplitter(fileLog, log);
            foreach (var pair in network.Team.GetNodesAsLogReplacements())
            {
                this.log.AddStringReplace(pair.Key, pair.Value);
            }
        }

        public void AddSlotTracker(SlotTrackerChainStateChangeHandler slotTracker)
        {
            slotTrackers.Add(slotTracker);
        }

        public void LogRequestId(byte[] requestId)
        {
            log.Log($"RequestId: '{requestId.ToHex()}'");
        }

        public void LogRequest(DateTime startUtc, byte[] requestId, Request request)
        {
            Add(startUtc, $"Storage Requested Event: '{requestId.ToHex()}' = {Environment.NewLine}" +
                $"{JsonConvert.SerializeObject(request, Formatting.Indented)}{Environment.NewLine}");
        }

        public void LogRequestCreated(RequestEvent requestEvent)
        {
            var msg = $"Storage request created: '{requestEvent.Request.RequestId.ToHex()}' = {Environment.NewLine}" +
                $"(cid: {requestEvent.Request.Cid}) " +
                $"{JsonConvert.SerializeObject(requestEvent.Request.Ask, Formatting.Indented)}{Environment.NewLine}";
            Add(requestEvent.Block, msg);
        }

        public void LogRequestCancelled(RequestEvent requestEvent)
        {
            Add(requestEvent.Block, "Expired");
        }

        public void LogRequestFailed(RequestEvent requestEvent)
        {
            Add(requestEvent.Block, "Failed");
        }

        public void LogRequestFinished(RequestEvent requestEvent)
        {
            Add(requestEvent.Block, "Finished");
        }

        public void LogRequestStarted(RequestEvent requestEvent)
        {
            Add(requestEvent.Block, "Started");
        }

        public void LogSlotFilled(RequestEvent requestEvent, EthAddress host, BigInteger slotIndex, bool isRepair)
        {
            Add(requestEvent.Block, $"Slot filled. Index: {slotIndex} Host: '{host}' isRepair: {isRepair}");
        }

        public void LogSlotFreed(RequestEvent requestEvent, BigInteger slotIndex)
        {
            Add(requestEvent.Block, $"Slot freed. Index: {slotIndex}");
        }

        public void LogSlotReservationsFull(RequestEvent requestEvent, BigInteger slotIndex)
        {
            Add(requestEvent.Block, $"Slot reservations full. Index: {slotIndex}");
        }

        public void WriteContractEvents()
        {
            var sorted = entries.OrderBy(e => e.Utc).ToArray();
            foreach (var e in sorted) Write(e);

            foreach (var slotTracker in slotTrackers)
            {
                WriteSlotTracker(slotTracker);
            }
        }

        public LogFile CreateNodeLogTargetFile(string node)
        {
            return log.CreateSubfile(node);
        }

        public void ShowOutputFiles(ILog console)
        {
            console.Log("Files in output folder:");
            var files = Directory.GetFiles(folder);
            foreach (var file in files) console.Log(file);
        }

        private void WriteSlotTracker(SlotTrackerChainStateChangeHandler slotTracker)
        {
            slotTracker.FinalizeReports();
            var slotReports = slotTracker.GetSlotReports();
            foreach (var report in slotReports)
            {
                log.Log("");
                report.WriteToLog(log);
            }
        }

        private void Write(Entry e)
        {
            log.Log($"[{Time.FormatTimestamp(e.Utc)}] {e.Msg}");
        }

        private void Add(DateTime utc, string msg)
        {
            entries.Add(new Entry(utc, msg));
        }

        private void Add(BlockTimeEntry blk, string msg)
        {
            entries.Add(new Entry(blk, msg));
        }
    }
}
