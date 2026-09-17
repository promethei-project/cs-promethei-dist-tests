using PrometheiClient.Hooks;
using Logging;
using OverwatchTranscript;
using Utils;

namespace PrometheiPlugin.OverwatchSupport
{
    public class PrometheiTranscriptWriter : IPrometheiHooksProvider
    {
        private const string PrometheiHeaderKey = "cdx_h";
        private readonly ILog log;
        private readonly PrometheiTranscriptWriterConfig config;
        private readonly ITranscriptWriter writer;
        private readonly PrometheiLogConverter converter;
        private readonly IdentityMap identityMap = new IdentityMap();
        private readonly KademliaPositionFinder positionFinder = new KademliaPositionFinder();

        public PrometheiTranscriptWriter(ILog log, PrometheiTranscriptWriterConfig config, ITranscriptWriter transcriptWriter)
        {
            this.log = log;
            this.config = config;
            writer = transcriptWriter;
            converter = new PrometheiLogConverter(writer, config, identityMap);
        }

        public void FinalizeWriter()
        {
            log.Log("Finalizing Promethei transcript...");

            writer.AddHeader(PrometheiHeaderKey, CreatePrometheiHeader());
            writer.Write(GetOutputFullPath());

            log.Log("Done");
        }

        private string GetOutputFullPath()
        {
            var outputPath = Path.GetDirectoryName(log.GetFullName());
            if (outputPath == null) throw new Exception("Logfile path is null");
            var filename = Path.GetFileNameWithoutExtension(log.GetFullName());
            if (string.IsNullOrEmpty(filename)) throw new Exception("Logfile name is null or empty");
            var outputFile = Path.Combine(outputPath, filename + "_" + config.OutputPath);
            if (!outputFile.EndsWith(".owts")) outputFile += ".owts";
            return outputFile;
        }

        public IPrometheiNodeHooks CreateHooks(string nodeName)
        {
            nodeName = Str.Between(nodeName, "'", "'");
            return new PrometheiNodeTranscriptWriter(writer, identityMap, nodeName);
        }

        public void IncludeFile(string filepath)
        {
            writer.IncludeArtifact(filepath);   
        }

        public void ProcessLogs(IDownloadedLog[] downloadedLogs)
        {
            foreach (var l in downloadedLogs)
            {
                log.Log("Include artifact: " + l.GetFilepath());
                writer.IncludeArtifact(l.GetFilepath());

                // Not all of these logs are necessarily Promethei logs.
                // Check, and process only the Promethei ones.
                if (IsPrometheiLog(l))
                {
                    log.Log("Processing Promethei log: " + l.GetFilepath());
                    converter.ProcessLog(l);
                }
            }
        }

        public void AddResult(bool success, string result)
        {
            writer.Add(DateTime.UtcNow, new OverwatchPrometheiEvent
            {
                NodeIdentity = -1,
                ScenarioFinished = new ScenarioFinishedEvent
                {
                    Success = success,
                    Result = result
                }
            });
        }

        private OverwatchPrometheiHeader CreatePrometheiHeader()
        {
            return new OverwatchPrometheiHeader
            {
                Nodes = positionFinder.DeterminePositions(identityMap.Get())
            };
        }

        private bool IsPrometheiLog(IDownloadedLog log)
        {
            return log.GetLinesContaining("Run Promethei node").Any();
        }
    }
}
