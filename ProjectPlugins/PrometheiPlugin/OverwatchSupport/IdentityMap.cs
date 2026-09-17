using PrometheiClient;

namespace PrometheiPlugin.OverwatchSupport
{
    public class IdentityMap
    {
        private readonly List<PrometheiNodeIdentity> nodes = new List<PrometheiNodeIdentity>();
        private readonly Dictionary<string, int> nameIndexMap = new Dictionary<string, int>();
        private readonly Dictionary<string, string> shortToLong = new Dictionary<string, string>();

        public void Add(string name, string peerId, string nodeId)
        {
            Add(new PrometheiNodeIdentity
            {
                Name = name,
                PeerId = peerId,
                NodeId = nodeId
            });

            nameIndexMap.Add(name, nameIndexMap.Count);
        }

        public void Add(PrometheiNodeIdentity identity)
        {
            if (string.IsNullOrWhiteSpace(identity.Name)) throw new Exception("Name required");
            if (string.IsNullOrWhiteSpace(identity.PeerId) || identity.PeerId.Length < 11) throw new Exception("PeerId invalid");
            if (string.IsNullOrWhiteSpace(identity.NodeId) || identity.NodeId.Length < 11) throw new Exception("NodeId invalid");

            nodes.Add(identity);

            shortToLong.Add(PrometheiUtils.ToShortId(identity.PeerId), identity.PeerId);
            shortToLong.Add(PrometheiUtils.ToNodeIdShortId(identity.NodeId), identity.NodeId);
        }

        public PrometheiNodeIdentity[] Get()
        {
            return nodes.ToArray();
        }

        public int GetIndex(string name)
        {
            return nameIndexMap[name];
        }

        public PrometheiNodeIdentity GetId(string name)
        {
            return nodes.Single(n => n.Name == name);
        }

        public string ReplaceShortIds(string value)
        {
            var result = value;
            foreach (var pair in shortToLong)
            {
                result = result.Replace(pair.Key, pair.Value);
            }
            return result;
        }

        public int Size
        {
            get { return nodes.Count; }
        }
    }
}
