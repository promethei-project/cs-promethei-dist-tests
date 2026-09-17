using Logging;
using MetricsServer;

namespace PrometheiGatewayService
{
    public class AppMetrics
    {
        private const string MetricPrefix = "promethei_gateway_";
        private readonly TimeSpan LoopDelay = TimeSpan.FromHours(0.5);
        private readonly ILog log;
        private readonly NodeSelector nodes;
        private readonly MetricsServer.MetricsServer server;
        private MetricsEvent manifestEvent = null!;
        private MetricsEvent dataEvent = null!;
        private MetricsEvent checkEvent = null!;

        public AppMetrics(ILog log, Configuration config, NodeSelector nodes)
        {
            this.log = log;
            this.nodes = nodes;
            server = new MetricsServer.MetricsServer(log, config.MetricsPort, MetricPrefix);
        }
        
        public void Initialize()
        {
            server.Start();

            manifestEvent = server.CreateEvent("manifests", "Number of requests to manifest endpoint");
            dataEvent = server.CreateEvent("data", "Number of requests to data endpoint");
            checkEvent = server.CreateEvent("checks", "Number of connection checks");

            Task.Run(Worker);
        }

        public void OnManifestRequest()
        {
            manifestEvent.Now();
        }

        public void OnDataRequest()
        {
            dataEvent.Now();
        }

        private void Worker()
        {
            while (true)
            {
                try
                {
                    WorkerStep();
                }
                catch (Exception ex)
                {
                    log.Error($"Error in worker thread: {ex}");
                }
                Thread.Sleep(LoopDelay);
            }
        }

        private void WorkerStep()
        {
            try
            {
                nodes.CheckOneNode().Wait();
                checkEvent.Now();
            }
            catch
            {
                // Quietly ignore. We'll see it in the metrics.
            }
        }
    }
}
