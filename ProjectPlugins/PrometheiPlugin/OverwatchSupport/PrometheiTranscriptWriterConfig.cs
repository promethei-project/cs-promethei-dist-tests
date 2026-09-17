namespace PrometheiPlugin.OverwatchSupport
{
    public class PrometheiTranscriptWriterConfig
    {
        public PrometheiTranscriptWriterConfig(string outputPath, bool includeBlockReceivedEvents)
        {
            OutputPath = outputPath;
            IncludeBlockReceivedEvents = includeBlockReceivedEvents;
        }

        public string OutputPath { get; }
        public bool IncludeBlockReceivedEvents { get; }
    }
}
