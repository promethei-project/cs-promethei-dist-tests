using PrometheiClient;

namespace PrometheiPlugin
{
    public class ProcessControlMap : IProcessControlFactory
    {
        private readonly Dictionary<string, IProcessControl> processControlMap = new Dictionary<string, IProcessControl>();

        public void Add(IPrometheiInstance instance, IProcessControl control)
        {
            processControlMap.Add(instance.Name, control);
        }

        public void Remove(IPrometheiInstance instance)
        {
            processControlMap.Remove(instance.Name);
        }

        public IProcessControl CreateProcessControl(IPrometheiInstance instance)
        {
            return Get(instance);
        }

        public IProcessControl Get(IPrometheiInstance instance)
        {
            return processControlMap[instance.Name];
        }

        public void StopAll()
        {
            var pcs = processControlMap.Values.ToArray();
            processControlMap.Clear();

            foreach (var c in pcs) c.Stop(waitTillStopped: true);
        }
    }
}
