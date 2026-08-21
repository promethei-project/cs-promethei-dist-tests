using k8s;

namespace KubernetesWorkflow
{
    public class K8sCluster
    {
        public K8sCluster(Configuration configuration)
        {
            Configuration = configuration;
        }

        public Configuration Configuration { get; }
        public string HostAddress { get; private set; } = string.Empty;

        private readonly object placementLock = new object();
        private string[] placementNodeNames = Array.Empty<string>();
        private long[] placementState = Array.Empty<long>();
        private readonly Dictionary<string, (long bytes, DateTime fetched)> nodeMemoryCache =
            new Dictionary<string, (long, DateTime)>();

        // Smoothed weighted round-robin shared across controllers so a whole
        // test's pods interleave by live available memory. The state is bound
        // to the node-name sequence: if the cluster's node set changes, the
        // running totals are reset instead of applying old currents to new
        // nodes.
        public int WeightedRoundRobinNext(string[] nodeNames, long[] weights)
        {
            lock (placementLock)
            {
                if (!nodeNames.SequenceEqual(placementNodeNames))
                {
                    placementNodeNames = (string[])nodeNames.Clone();
                    placementState = new long[nodeNames.Length];
                }

                long total = weights.Sum();
                long best = long.MinValue;
                var bestIdx = 0;
                for (var i = 0; i < weights.Length; i++)
                {
                    placementState[i] += weights[i];
                    if (placementState[i] > best)
                    {
                        best = placementState[i];
                        bestIdx = i;
                    }
                }
                placementState[bestIdx] -= total;
                return bestIdx;
            }
        }

        public long? GetCachedNodeMemory(string nodeName)
        {
            lock (placementLock)
            {
                if (nodeMemoryCache.TryGetValue(nodeName, out var entry)
                    && DateTime.UtcNow - entry.fetched < TimeSpan.FromSeconds(30))
                {
                    return entry.bytes;
                }
                return null;
            }
        }

        public void SetCachedNodeMemory(string nodeName, long bytes)
        {
            lock (placementLock)
            {
                nodeMemoryCache[nodeName] = (bytes, DateTime.UtcNow);
            }
        }

        public KubernetesClientConfiguration GetK8sClientConfig()
        {
            var config = GetConfig();
            UpdateHostAddress(config);
            config.SkipTlsVerify = true; // Required for operation on Wings cluster.
            return config;
        }

        public TimeSpan K8sOperationTimeout()
        {
            return Configuration.OperationTimeout;
        }

        public TimeSpan K8sOperationRetryDelay()
        {
            return Configuration.RetryDelay;
        }

        private KubernetesClientConfiguration GetConfig()
        {
            if (Configuration.KubeConfigFile != null)
            {
                return KubernetesClientConfiguration.BuildConfigFromConfigFile(Configuration.KubeConfigFile);
            }
            else
            {
                return KubernetesClientConfiguration.BuildDefaultConfig();
            }
        }

        private void UpdateHostAddress(KubernetesClientConfiguration config)
        {
            var host = config.Host.Replace("https://", "");
            if (host.Contains(":"))
            {
                HostAddress = "http://" + host.Substring(0, host.IndexOf(':'));
            }
            else
            {
                HostAddress = config.Host;
            }
        }
    }
}
