using BlockchainUtils;
using PrometheiContractsPlugin.ChainMonitor;
using PrometheiContractsPlugin.Marketplace;
using DiscordRewards;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Globalization;
using System.Numerics;
using Utils;

namespace TestNetRewarder
{
    public class EventsFormatter : IChainStateChangeHandler
    {
        private static readonly string nl = Environment.NewLine;
        private readonly List<ChainEventMessage> events = new List<ChainEventMessage>();
        private readonly List<string> errors = new List<string>();
        private readonly EmojiMaps emojiMaps = new EmojiMaps();
        private readonly string periodDuration;

        public EventsFormatter(ContentInformationLookup lookup, MarketplaceConfig marketplaceConfig)
        {
            this.lookup = lookup;
            periodDuration = Time.FormatDuration(marketplaceConfig.PeriodDuration);
        }

        public ChainEventMessage[] GetInitializationEvents(Configuration config, int numRecoveredRequests)
        {
            return [
                FormatBlock(0, 
                    new MsgBlock(
                        header: "Bot initializing...",
                        content: [
                            $"History-check start (UTC) = {Time.FormatTimestamp(config.HistoryStartUtc)}",
                            $"Update interval = {Time.FormatDuration(config.Interval)}",
                            $"Found {numRecoveredRequests} running storage requests at start-up"
                        ],
                        footer: string.Empty
                    )
                )
            ];
        }

        public ChainEventMessage[] GetEvents()
        {
            var result = events.ToArray();
            events.Clear();
            return result;
        }

        public string[] GetErrors()
        {
            var result = errors.ToArray();
            errors.Clear();
            return result;
        }

        public void OnNewRequest(RequestEvent requestEvent)
        {
            var request = requestEvent.Request;
            var cid = request.Cid;
            var content = new List<string>()
            {
                $"Client: {request.Client}",
                $"Content: {cid}",
            };
            content.AddRange(lookup.DescribeManifest(cid.Id));
            content.AddRange([
                $"Duration: {Time.FormatDuration(request.Ask.Duration)}",
                $"Expiry: {Time.FormatDuration(request.Expiry)}",
                $"CollateralPerByte: {request.Ask.CollateralPerByte}",
                $"PricePerBytePerSecond: {request.Ask.PricePerBytePerSecond}",
                $"Number of Slots: {request.Ask.Slots}",
                $"Slot Tolerance: {request.Ask.MaxSlotLoss}",
                $"Slot Size: {request.Ask.SlotSize}",
                $"Proof Probability: 1 / {request.Ask.ProofProbability} every {periodDuration}"
            ]);

            AddExtendsContractInfo(request, content);

            AddRequestBlock(requestEvent, GetNewOrExtendEmoji(request),
                new MsgBlock(
                    header: GetNewOrExtendHeader(request),
                    content: content.ToArray(),
                    footer: string.Empty
                )
            );
        }

        public void OnRequestCancelled(RequestEvent requestEvent)
        {
            AddRequestBlock(requestEvent, GetCancelledEmoji(requestEvent.Request),
                new MsgBlock(
                    header: GetCancelledHeader(requestEvent.Request),
                    content: [],
                    footer: string.Empty
                )
            );
        }

        public void OnRequestFailed(RequestEvent requestEvent)
        {
            AddRequestBlock(requestEvent, emojiMaps.Failed,
                new MsgBlock(
                    header: "Failed",
                    content: [],
                    footer: string.Empty
                )
            );
        }

        public void OnRequestFinished(RequestEvent requestEvent, IChainStateRequest? extendedBy)
        {
            AddRequestBlock(requestEvent, GetFinishedEmoji(extendedBy),
                new MsgBlock(
                    header: "Finished",
                    content: GetFinishedContent(extendedBy),
                    footer: string.Empty
                )
            );
        }

        public void OnRequestFulfilled(RequestEvent requestEvent)
        {
            var request = requestEvent.Request;
            var cid = request.Cid.Id;

            AddRequestBlock(requestEvent, GetStartedOrExtendEmoji(request),
                new MsgBlock(
                    header: GetStartedOrExtendHeader(request),
                    content: lookup.DescribeManifest(cid),
                    footer: lookup.GenerateDownloadLink(cid)
                )
            );
        }

        public void OnSlotFilled(RequestEvent requestEvent, EthAddress host, BigInteger slotIndex, bool isRepair)
        {
            var request = requestEvent.Request;
            var content = new List<string>
            {
                $"Slot Index: {slotIndex}",
                $"Host: {host}"
            };

            AddPreviousHostInfoIfAble(request, slotIndex, content);

            AddRequestBlock(requestEvent, GetSlotFilledIcon(request, isRepair),
                new MsgBlock(
                    header: GetSlotFilledTitle(isRepair),
                    content: content.ToArray(),
                    footer: string.Empty
                )
            );
        }

        public void OnSlotFreed(RequestEvent requestEvent, BigInteger slotIndex)
        {
            AddRequestBlock(requestEvent, emojiMaps.SlotFreed,
                new MsgBlock(
                    header: "Slot Freed",
                    content: [$"Slot Index: {slotIndex}"],
                    footer: string.Empty
                )
            );
        }

        public void OnSlotReservationsFull(RequestEvent requestEvent, BigInteger slotIndex)
        {
            AddRequestBlock(requestEvent, emojiMaps.SlotReservationsFull,
                new MsgBlock(
                    header: "Slot Reservations Full",
                    content: [$"Slot Index: {slotIndex}"],
                    footer: string.Empty
                )
            );
        }

        public void OnProofSubmitted(BlockTimeEntry block, string id)
        {
            // There are a lot of these.
        }

        public void OnError(string msg)
        {
            errors.Add(msg);
        }

        private readonly List<PeriodReportWithMisses> reports = new List<PeriodReportWithMisses>();
        private readonly ContentInformationLookup lookup;

        public void OnPeriodReport(PeriodReportWithMisses report)
        {
            reports.Add(report);

            if (ShouldPublishPeriodReports())
            {
                PublishPeriodReports();
                reports.Clear();
            }
        }

        private string[] GetFinishedContent(IChainStateRequest? extendedBy)
        {
            if (extendedBy == null) return Array.Empty<string>();
            return [$"Extended by: {FormatRequestId(extendedBy.RequestId)}"];
        }

        private string GetFinishedEmoji(IChainStateRequest? extendedBy)
        {
            if (extendedBy == null) return emojiMaps.Finished;
            return emojiMaps.FinishedButExtended;
        }

        private void AddPreviousHostInfoIfAble(IChainStateRequest request, BigInteger slotIndex, List<string> content)
        {
            if (request.ExtendsExistingContract == null) return;
            var existing = request.ExtendsExistingContract;
            var previousHost = existing.Hosts.GetHost((int)slotIndex);
            if (previousHost == null) return;
            content.Add($"Previous host: {previousHost}");
        }

        private void AddExtendsContractInfo(IChainStateRequest request, List<string> content)
        {
            if (request.ExtendsExistingContract == null) return;
            content.Add($"Extends previous contract: {FormatRequestId(request.ExtendsExistingContract.RequestId)}");
        }

        private string GetNewOrExtendEmoji(IChainStateRequest request)
        {
            if (request.ExtendsExistingContract != null) return emojiMaps.ExtendRequest;
            return emojiMaps.NewRequest;
        }

        private string GetNewOrExtendHeader(IChainStateRequest request)
        {
            if (request.ExtendsExistingContract != null) return "Renewed Request";
            return "New Request";
        }

        private string GetStartedOrExtendEmoji(IChainStateRequest request)
        {
            if (request.ExtendsExistingContract != null) return emojiMaps.Renewed;
            return emojiMaps.Started;
        }

        private string GetStartedOrExtendHeader(IChainStateRequest request)
        {
            if (request.ExtendsExistingContract != null) return "Renewed";
            return "Started";
        }

        private string GetCancelledHeader(IChainStateRequest request)
        {
            if (request.ExtendsExistingContract != null) return "Renew Cancelled";
            return "Cancelled";
        }

        private string GetCancelledEmoji(IChainStateRequest request)
        {
            if (request.ExtendsExistingContract != null) return emojiMaps.CancelledRenewed;
            return emojiMaps.CancelledNew;
        }

        private bool ShouldPublishPeriodReports()
        {
            if (reports.Count > 960) return true;
            // At a rate of 30-seconds per period (arbitrum testnet config)
            // This will yield 3 reports per day.

            var totalMissed = 0;
            foreach (var r in reports)
            {
                if (r.MissedSlots.Length > 10) return true;
                // If any 1 period has more than 10 missed proofs, report.

                totalMissed += r.MissedSlots.Length;
            }

            if (totalMissed > 100) return true;
            // If there are more than 100 missed proof reports collected in total, report.

            return false;
        }

        private void PublishPeriodReports()
        {
            var first = reports.Min(r => r.PeriodReport.Period.PeriodNumber);
            var last = reports.Max(r => r.PeriodReport.Period.PeriodNumber);

            var lines = new List<string>()
            {
                $"For proving periods [{first} to {last}]"
            };
            
            var totalMissed = 0;
            foreach (var report in reports)
            {
                var missed = report.MissedSlots.Length;
                totalMissed += missed;

                var msg = $"In period {report.PeriodReport.Period.PeriodNumber}: ";

                if (missed > 10)
                {
                    lines.Add($"{msg} {missed} storage proofs were missed! {emojiMaps.ManyProofsMissed}");
                }
                if (missed > 0)
                {
                    lines.Add(msg);
                    foreach (var s in report.MissedSlots)
                    {
                        var host = s.SlotReport.Host.AsStr();
                        var request = FormatRequestId(s.Request);
                        var idx = s.SlotReport.Index;
                        var marked = s.SlotReport.MarkedAsMissing;
                        lines.Add($" - '{host}' missed a proof for request {request} (slotIndex:{idx}, marked:{marked})");
                    }
                }
            }
            if (totalMissed == 0)
            {
                lines.Add($"No proofs were missed {emojiMaps.NoProofsMissed}");
            }

            AddBlock(0,
                new MsgBlock(
                    header: $"{emojiMaps.ProofReport} **Proof system report**",
                    content: lines.ToArray(),
                    footer: string.Empty
                )
            );
        }

        private string GetSlotFilledIcon(IChainStateRequest request, bool isRepair)
        {
            if (isRepair) return emojiMaps.SlotRepaired;
            if (request.ExtendsExistingContract != null) return emojiMaps.SlotReFilled;
            return emojiMaps.SlotFilled;
        }

        private string GetSlotFilledTitle(bool isRepair)
        {
            if (isRepair) return $"Slot Repaired";
            return $"Slot Filled";
        }

        private void AddRequestBlock(RequestEvent requestEvent, string icon, MsgBlock msgBlock)
        {
            var blockNumber = $"[{requestEvent.Block.BlockNumber} {FormatDateTime(requestEvent.Block.Utc)}]";
            var title = $"{blockNumber} {icon} **{msgBlock.Header}** {FormatRequestId(requestEvent)}";
            var requestMsgBlock = new MsgBlock(title, msgBlock.Content, msgBlock.Footer);

            AddBlock(requestEvent.Block.BlockNumber, requestMsgBlock);
        }

        private void AddBlock(ulong blockNumber, MsgBlock msgBlock)
        {
            events.Add(FormatBlock(blockNumber, msgBlock));
        }

        private ChainEventMessage FormatBlock(ulong blockNumber, MsgBlock msgBlock)
        {
            var msg = FormatBlockMessage(msgBlock);
            return new ChainEventMessage
            {
                BlockNumber = blockNumber,
                Message = msg
            };
        }

        private class MsgBlock
        {
            public MsgBlock(string header, string[] content, string footer)
            {
                Header = header;
                Content = content;
                Footer = footer;
            }

            public string Header { get; }
            public string[] Content { get; }
            public string Footer { get; }
        }

        private string FormatBlockMessage(MsgBlock msgBlock)
        {
            var result = new List<string>();
            if (!string.IsNullOrEmpty(msgBlock.Header))
            {
                result.Add(msgBlock.Header);
            }
            if (msgBlock.Content != null &&  msgBlock.Content.Length > 0)
            {
                result.Add("```");
                result.AddRange(msgBlock.Content);
                result.Add("```");
            }
            if (!string.IsNullOrEmpty(msgBlock.Footer))
            {
                result.Add(msgBlock.Footer);
            }
            return string.Join(nl, result) + nl + nl;
        }

        private string FormatDateTime(DateTime utc)
        {
            return utc.ToString("yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture);
        }

        private string FormatRequestId(RequestEvent requestEvent)
        {
            return FormatRequestId(requestEvent.Request);
        }

        private string FormatRequestId(IChainStateRequest request)
        {
            return FormatRequestId(request.RequestId);
        }

        private string FormatRequestId(byte[] id)
        {
            var str = id.ToHex();
            return
                $"({emojiMaps.StringToEmojis(str, 3)})" +
                $"`{str}`";
        }
    }
}
