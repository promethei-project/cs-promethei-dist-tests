using PrometheiClient;
using GethPlugin;

namespace PrometheiContractsPlugin
{
    public interface IPrometheiContractsSetup
    {
        IPrometheiContractsSetup WithRpcNode(IGethNode node);
        IPrometheiContractsSetup WithVersionInfo(DebugInfoVersion info);
        IPrometheiContractsSetup WithRequestsCache(IRequestsCache requestsCache);
        IPrometheiContractsSetup WithMaxReservationsOverride(int maxReservations);
    }

    public class PrometheiContractsSetupBuilder : IPrometheiContractsSetup
    {
        private int? maxReservationsOverride = null;
        private IRequestsCache requestsCache = new NullRequestsCache();
        private IGethNode? rpcNode = null;
        private DebugInfoVersion? infoVersion = null;

        public IPrometheiContractsSetup WithMaxReservationsOverride(int maxReservations)
        {
            maxReservationsOverride = maxReservations;
            return this;
        }

        public IPrometheiContractsSetup WithRequestsCache(IRequestsCache requestsCache)
        {
            this.requestsCache = requestsCache;
            return this;
        }

        public IPrometheiContractsSetup WithRpcNode(IGethNode node)
        {
            rpcNode = node;
            return this;
        }

        public IPrometheiContractsSetup WithVersionInfo(DebugInfoVersion info)
        {
            infoVersion = info;
            return this;
        }

        public PrometheiContractsSetup Build()
        {
            if (rpcNode == null) throw new Exception("PrometheiContracts requires RPC node. Use '.WithRpcNode(...)'");
            if (infoVersion == null) throw new Exception("PrometheiContracts requires Promethei version information. Use '.WithVersionInfo(...)'");
            return new PrometheiContractsSetup(rpcNode, infoVersion, requestsCache, maxReservationsOverride);
        }
    }

    public class PrometheiContractsSetup
    {
        public PrometheiContractsSetup(IGethNode rpcNode, DebugInfoVersion infoVersion, IRequestsCache requestsCache, int? maxReservationsOverride)
        {
            RpcNode = rpcNode;
            InfoVersion = infoVersion;
            RequestsCache = requestsCache;
            MaxReservationsOverride = maxReservationsOverride;
        }

        public IGethNode RpcNode { get; }
        public DebugInfoVersion InfoVersion { get; }
        public IRequestsCache RequestsCache { get; }
        public int? MaxReservationsOverride { get; }
    }
}
