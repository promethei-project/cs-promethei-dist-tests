using PrometheiClient;
using PrometheiClient.Hooks;
using Core;
using Logging;

namespace PrometheiPlugin
{
    public class PrometheiWrapper
    {
        private readonly IPluginTools pluginTools;
        private readonly ProcessControlMap processControlMap;
        private readonly PrometheiHooksFactory hooksFactory;
        private DebugInfoVersion? versionResponse;

        public PrometheiWrapper(IPluginTools pluginTools, ProcessControlMap processControlMap, PrometheiHooksFactory hooksFactory)
        {
            this.pluginTools = pluginTools;
            this.processControlMap = processControlMap;
            this.hooksFactory = hooksFactory;
        }

        public string GetPrometheiId()
        {
            if (versionResponse != null) return versionResponse.Version;
            return "unknown";
        }

        public string GetPrometheiRevision()
        {
            if (versionResponse != null) return versionResponse.Revision;
            return "unknown";
        }

        public IPrometheiNodeGroup WrapPrometheiInstances(IPrometheiInstance[] instances)
        {
            var prometheiNodeFactory = new PrometheiNodeFactory(
                log: pluginTools.GetLog(),
                fileManager: pluginTools.GetFileManager(),
                hooksFactory: hooksFactory,
                httpFactory: pluginTools,
                processControlFactory: processControlMap);

            var group = CreatePrometheiGroup(instances, prometheiNodeFactory);

            pluginTools.GetLog().Log($"Promethei version: {group.Version}");
            versionResponse = group.Version;

            return group;
        }

        private PrometheiNodeGroup CreatePrometheiGroup(IPrometheiInstance[] instances, PrometheiNodeFactory prometheiNodeFactory)
        {
            var nodes = instances.Select(prometheiNodeFactory.CreatePrometheiNode).ToArray();
            var group = new PrometheiNodeGroup(pluginTools, nodes);

            try
            {
                Stopwatch.Measure(pluginTools.GetLog(), "EnsureOnline", group.EnsureOnline);
            }
            catch
            {
                PrometheiNodesNotOnline(instances);
                throw;
            }

            return group;
        }

        private void PrometheiNodesNotOnline(IPrometheiInstance[] instances)
        {
            pluginTools.GetLog().Log("Promethei nodes failed to start");
            var log = pluginTools.GetLog();
            foreach (var i in instances)
            {
                var pc = processControlMap.Get(i);
                pc.DownloadLog(log.CreateSubfile(i.Name + "_failed_to_start"));
            }
        }
    }
}
