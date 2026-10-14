namespace KubernetesWorkflow.Types
{
    public class K8sNodeLabel
    {
        public K8sNodeLabel(string key, string value, string nodeName = "", string internalIp = "")
        {
            Key = key;
            Value = value;
            NodeName = nodeName;
            InternalIp = internalIp;
        }

        public string Key { get; }
        public string Value { get; }
        public string NodeName { get; }
        public string InternalIp { get; }
    }
}
