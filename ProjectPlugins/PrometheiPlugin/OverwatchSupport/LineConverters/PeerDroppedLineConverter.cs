using PrometheiClient;

namespace PrometheiPlugin.OverwatchSupport.LineConverters
{
    public class PeerDroppedLineConverter : ILineConverter
    {
        public string Interest => "Dropping peer";

        public void Process(PrometheiLogLine line, Action<Action<OverwatchPrometheiEvent>> addEvent)
        {
            var peerId = line.Attributes["peer"];

            addEvent(e =>
            {
                e.PeerDropped = new PeerDroppedEvent
                {
                    DroppedPeerId = peerId
                };
            });
        }
    }
}
