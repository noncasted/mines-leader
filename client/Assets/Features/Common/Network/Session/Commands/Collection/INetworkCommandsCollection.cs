using Shared;

namespace Common.Network
{
    public interface INetworkCommandsCollection
    {
        void Add(INetworkCommand command);
        INetworkCommand Get(INetworkContext context);
    }
}