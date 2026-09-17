using PrometheiClient.Hooks;
using FileUtils;
using Logging;
using WebUtils;

namespace PrometheiClient
{
    public class PrometheiNodeFactory
    {
        private readonly ILog log;
        private readonly IFileManager fileManager;
        private readonly PrometheiHooksFactory hooksFactory;
        private readonly IHttpFactory httpFactory;
        private readonly IProcessControlFactory processControlFactory;

        public PrometheiNodeFactory(ILog log, IFileManager fileManager, PrometheiHooksFactory hooksFactory, IHttpFactory httpFactory, IProcessControlFactory processControlFactory)
        {
            this.log = log;
            this.fileManager = fileManager;
            this.hooksFactory = hooksFactory;
            this.httpFactory = httpFactory;
            this.processControlFactory = processControlFactory;
        }

        public PrometheiNodeFactory(ILog log, HttpFactory httpFactory, string dataDir)
            : this(log, new FileManager(log, dataDir), new PrometheiHooksFactory(), httpFactory, new DoNothingProcessControlFactory())
        {
        }

        public PrometheiNodeFactory(ILog log, string dataDir)
            : this(log, new HttpFactory(log), dataDir)
        {
        }

        public IPrometheiNode CreatePrometheiNode(IPrometheiInstance instance)
        {
            var nodeLog = new LogPrefixer(log, $"({instance.Name}) ");
            var processControl = processControlFactory.CreateProcessControl(instance);
            var access = new PrometheiAccess(nodeLog, httpFactory, processControl, instance);
            var hooks = hooksFactory.CreateHooks(access.GetName());
            var marketplaceAccess = CreateMarketplaceAccess(instance, nodeLog, access, hooks);
            var node =  new PrometheiNode(nodeLog, access, fileManager, marketplaceAccess, hooks);
            node.Initialize();
            return node;
        }

        private IMarketplaceAccess CreateMarketplaceAccess(IPrometheiInstance instance, ILog nodeLog, PrometheiAccess access, IPrometheiNodeHooks hooks)
        {
            if (instance.EthAccount == null) return new MarketplaceUnavailable();
            return new MarketplaceAccess(nodeLog, access, hooks);
        }
    }
}
