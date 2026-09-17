using PrometheiClient;

namespace PrometheiPlugin
{
    public interface IPrometheiStarter
    {
        IPrometheiInstance[] BringOnline(PrometheiSetup prometheiSetup);
        void Decommission();
    }
}
