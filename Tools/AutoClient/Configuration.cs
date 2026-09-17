using ArgsUniform;

namespace AutoClient
{
    public class Configuration
    {
        [Uniform("promethei-endpoints", "ce", "PROMETHEIENDPOINTS", false, "Promethei endpoints. Semi-colon separated. (default 'http://localhost:8080')")]
        public string PrometheiEndpoints { get; set; } =
            "http://localhost:8080" + ";" +
            "http://localhost:8081" + ";" +
            "http://localhost:8082" + ";" +
            "http://localhost:8083" + ";" +
            "http://localhost:8084" + ";" +
            "http://localhost:8085" + ";" +
            "http://localhost:8086" + ";" +
            "http://localhost:8087";

        [Uniform("datapath", "dp", "DATAPATH", false, "Root path where all data files will be saved.")]
        public string DataPath { get; set; } = "datapath";
        
        [Uniform("metrics-port", "mp", "METRICSPORT", false, "Local port for metrics endpoint. (default: '8008')")]
        public int MetricsPort { get; set; } = 8008;
        
        [Uniform("contract-duration", "cd", "CONTRACTDURATION", false, "contract duration in minutes. (default 6 days) If two numbers are provided comma-separated, they are used as min/max for random duration values.")]
        public string ContractDurationStr { get; set; } = "7200,9360"; // 7200 = 5 days, 8640 = 6 days

        [Uniform("contract-expiry", "ce", "CONTRACTEXPIRY", false, "contract expiry in minutes. (default 15 minutes)")]
        public int ContractExpiryMinutes { get; set; } = 15;

        [Uniform("durability-values", "dv", "DURABILITYVALUES", false, "Semi-colon separated values formatted in \"(numHosts,tolerance,proofProbability)\" segments. (default \"(4,2,50)\")")]
        public string DurabilityValuesStr { get; set; } = "(4, 2, 50)";

        [Uniform("price","p", "PRICE", false, "Price per byte per second in TSTWEI. (default 1000)")]
        public int PricePerBytePerSecond { get; set; } = 1000;

        [Uniform("collateral", "c", "COLLATERAL", false, "Required collateral per byte in TSTWEI. (default 1)")]
        public int CollateralPerByte { get; set; } = 1;

        [Uniform("folderToStore", "fts", "FOLDERTOSTORE", false, "When set, autoclient will attempt to upload and purchase storage for every non-JSON file in the provided folder.")]
        public string FolderToStore { get; set; } = "/data/EthereumMainnetPreMergeEraFiles";

        [Uniform("slowModeDelayMinutes", "smdm", "SLOWMODEDELAYMINUTES", false, "When contract failure threshold is reached, slow down process for each file by this amount of minutes.")]
        public int SlowModeDelayMinutes { get; set; } = 30 * 1;

        public string LogPath
        {
            get
            {
                return Path.Combine(DataPath, "logs");
            }
        }

        private DurabilityValues[] durabilityValues = [];
        public DurabilityValues[] DurabilityValues
        {
            get
            {
                if (durabilityValues.Length == 0) ParseDurabilityValues();
                return durabilityValues;
            }
        }

        private TimeSpan[] durations = [];
        public TimeSpan[] Durations
        {
            get
            {
                if (durations.Length == 0) ParseDurationMinutes();
                return durations;
            }
        }

        private void ParseDurationMinutes()
        {
            var tokens = ContractDurationStr.Split(",", StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 1 || tokens.Length > 2) throw new Exception("Expected 1 or 2 comma-separated int values for contract-duration options.");
            durations = tokens.Select(t => TimeSpan.FromMinutes(Convert.ToInt32(t))).Order().ToArray();
            if (durations[0] < TimeSpan.FromMinutes(10)) throw new Exception("contract-duration option must be 10 or greater.");
        }

        private void ParseDurabilityValues()
        {
            var str = DurabilityValuesStr
                .Replace(Environment.NewLine, "")
                .Replace(" ", "")
                .Replace("(", "")
                .Replace(")", "");
            var tokens = str.Split(";", StringSplitOptions.RemoveEmptyEntries);
            durabilityValues = tokens.Select(ParseDurabilityValuesEntry).ToArray();
        }

        private DurabilityValues ParseDurabilityValuesEntry(string str)
        {
            var tokens = str.Split(",", StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != 3) throw new Exception($"Malformed DurabilityValues entry: '{str}'. Expecting 3 values: '(numHosts, tolerance, proofProbability)'.");
            return new DurabilityValues(
                Convert.ToInt32(tokens[0]),
                Convert.ToInt32(tokens[1]),
                Convert.ToInt32(tokens[2])
            );
        }
    }

    public class DurabilityValues
    {
        public DurabilityValues(int nodes, int tolerance, int proofProbability)
        {
            Nodes = nodes;
            Tolerance = tolerance;
            ProofProbability = proofProbability;
        }

        public int Nodes { get; }
        public int Tolerance { get; }
        public int ProofProbability { get; }
    }
}
