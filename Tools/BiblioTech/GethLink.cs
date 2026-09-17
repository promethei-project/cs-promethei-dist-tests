using PrometheiContractsPlugin;
using GethPlugin;

namespace BiblioTech
{
    public class GethLink
    {
        private GethLink(IGethNode node, IPrometheiContracts contracts)
        {
            Node = node;
            Contracts = contracts;
        }

        public IGethNode Node { get; }
        public IPrometheiContracts Contracts { get; }

        public static GethLink? Create()
        {
            var gethConnector = GetGeth();
            if (gethConnector == null) return null;

            var gethNode = gethConnector.GethNode;
            var contracts = gethConnector.PrometheiContracts;
            return new GethLink(gethNode, contracts);
        }

        private static GethConnector.GethConnector? GetGeth()
        {
            try
            {
                return GethConnector.GethConnector.Initialize(Program.Log, Program.Network);
            }
            catch (Exception ex)
            {
                Program.Log.Error("Failed to initialize geth connector: " + ex);
                return null;
            }
        }
    }
}
