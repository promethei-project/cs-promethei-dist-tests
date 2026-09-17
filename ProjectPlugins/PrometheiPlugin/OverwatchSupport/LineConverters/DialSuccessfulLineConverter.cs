using PrometheiClient;

namespace PrometheiPlugin.OverwatchSupport.LineConverters
{
    public class DialSuccessfulLineConverter : ILineConverter
    {
        public string Interest => "Dial successful";

        public void Process(PrometheiLogLine line, Action<Action<OverwatchPrometheiEvent>> addEvent)
        {
            var peerId = line.Attributes["peerId"];

            addEvent(e =>
            {
                e.DialSuccessful = new PeerDialSuccessfulEvent
                {
                    TargetPeerId = peerId
                };
            });
        }
    }
}
