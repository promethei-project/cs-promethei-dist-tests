using PrometheiClient;
using Core;
using System.Collections;
using Utils;

namespace PrometheiPlugin
{
    public interface IPrometheiNodeGroup : IEnumerable<IPrometheiNode>, IHasManyMetricScrapeTargets
    {
        void Stop(bool waitTillStopped);
        IPrometheiNode this[int index] { get; }
    }

    public class PrometheiNodeGroup : IPrometheiNodeGroup
    {
        private readonly IPrometheiNode[] nodes;

        public PrometheiNodeGroup(IPluginTools tools, IPrometheiNode[] nodes)
        {
            this.nodes = nodes;
            Version = new DebugInfoVersion();
        }

        public IPrometheiNode this[int index]
        {
            get
            {
                return Nodes[index];
            }
        }

        public void Stop(bool waitTillStopped)
        {
            foreach (var node in Nodes) node.Stop(waitTillStopped);
        }

        public void Stop(PrometheiNode node, bool waitTillStopped)
        {
            node.Stop(waitTillStopped);
        }

        public IPrometheiNode[] Nodes => nodes;
        public DebugInfoVersion Version { get; private set; }

        public Address[] GetMetricsScrapeTargets()
        {
            return Nodes.Select(n => n.GetMetricsScrapeTarget()).ToArray();
        }

        public IEnumerator<IPrometheiNode> GetEnumerator()
        {
            return Nodes.Cast<IPrometheiNode>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Nodes.GetEnumerator();
        }

        public string Names()
        {
            return $"[{string.Join(",", Nodes.Select(n => n.GetName()))}]";
        }

        public override string ToString()
        {
            return Names();
        }

        public void EnsureOnline()
        {
            var versionResponses = Nodes.Select(n => n.Version);

            var first = versionResponses.First();
            if (!versionResponses.All(v => v.Version == first.Version && v.Revision == first.Revision))
            {
                throw new Exception("Inconsistent version information received from one or more Promethei nodes: " +
                    string.Join(",", versionResponses.Select(v => v.ToString())));
            }

            Version = first;
        }
    }

    public static class PrometheiNodeGroupExtensions
    {
        public static string Names(this IPrometheiNode[] nodes)
        {
            return $"[{string.Join(",", nodes.Select(n => n.GetName()))}]";
        }

        public static string Names(this List<IPrometheiNode> nodes)
        {
            return $"[{string.Join(",", nodes.Select(n => n.GetName()))}]";
        }
    }
}
