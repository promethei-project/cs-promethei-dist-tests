using PrometheiClient;
using PrometheiPlugin.OverwatchSupport;
using OverwatchTranscript;

namespace TranscriptAnalysis.Receivers
{
    public class LogReplaceReceiver : BaseReceiver<OverwatchPrometheiEvent>
    {
        public override string Name => "LogReplacer";

        private readonly List<string> seen = new List<string>();

        public override void Receive(ActivateEvent<OverwatchPrometheiEvent> @event)
        {
            var peerId = GetPeerId(@event.Payload.NodeIdentity);
            var name = GetName(@event.Payload.NodeIdentity);
            if (peerId == null) return;
            if (name == null) return;

            if (!seen.Contains(peerId))
            {
                seen.Add(peerId);

                log.AddStringReplace(peerId, name);
                log.AddStringReplace(PrometheiUtils.ToShortId(peerId), name);
            }
        }

        public override void Finish()
        {
        }
    }
}
