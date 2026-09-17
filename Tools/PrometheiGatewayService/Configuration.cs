using ArgsUniform;

namespace PrometheiGatewayService
{
    public class Configuration
    {
        [Uniform("listen-url", "l", "LISTENURL", false, "API server listen URL. (default: 'https://0.0.0.0:8080')")]
        public string ListenUrl { get; set; } = "https://0.0.0.0:8080";

        [Uniform("metrics-port", "mp", "METRICSPORT", false, "Local port for metrics endpoint. (default: '8008')")]
        public int MetricsPort { get; set; } = 8008;

        [Uniform("promethei-endpoints", "ce", "PROMETHEIENDPOINTS", true, "Promethei endpoints. Semi-colon separated.")]
        public string PrometheiEndpoints { get; set; } = "";

        [Uniform("datapath", "dp", "DATAPATH", false, "Root path where all data files will be saved. (default: 'datapath'")]
        public string DataPath { get; set; } = "datapath";

        [Uniform("timeout", "t", "TIMEOUT", false, "Timeout (in minutes) used for outgoing HTTP requests. (default: 30)")]
        public int RequestTimeoutMinutes { get; set; } = 30;
    }
}
