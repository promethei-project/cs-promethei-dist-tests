using System.Diagnostics;
using PrometheiClient;
using PrometheiNetworkConfig;
using Logging;
using MetricsServer;

namespace EphemeralClient
{
    public class LocalNode
    {
        private readonly ILog log;
        private readonly MetricsEvent failedToStart;

        public LocalNode(ILog log, MetricsServer.MetricsServer metricsServer)
        {
            this.log = new LogPrefixer(log, "(LocalNode)");
            failedToStart = metricsServer.CreateEvent("node_start_failed", "failed to start node");
        }

        public void Initialize(PrometheiNetwork network)
        {
            var filename = "docker-compose.yaml";
            if (!File.Exists(filename))
            {
                log.Error($"File '{filename}' does not exist.");
                throw new FileNotFoundException(filename);
            }

            var targetLine = "      - NETWORK=";
            var lines = File.ReadAllLines(filename);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(targetLine))
                {
                    // In-place update and done.
                    lines[i] = targetLine + network.Name;
                    File.WriteAllLines(filename, lines);
                    return;
                }
            }

            log.Error($"Did not find target line '{targetLine}' in file '{filename}'");
            throw new InvalidDataException();
        }

        public IPrometheiNode Start()
        {
            try
            {
                log.Log("Starting...");
                Process.Start("docker", "compose up -d");

                Thread.Sleep(TimeSpan.FromSeconds(40.0));

                var factory = new PrometheiNodeFactory(log, "datadir");
                var instance = PrometheiInstance.CreateFromApiEndpoint(
                    "name",
                    new Utils.Address("name", "http://localhost", 8089)
                );
                return factory.CreatePrometheiNode(instance);
            }
            catch (Exception ex)
            {
                log.Error($"Failed to start node: {ex}");
                failedToStart.Now();
                throw;
            }
        }

        public void StopAndClean()
        {
            log.Log("Stopping...");
            Process.Start("docker", "compose down");

            Thread.Sleep(TimeSpan.FromSeconds(20.0));

            log.Log("Cleaning up...");
            Directory.Delete("volumes", true);
        }
    }
}
