using Global.Backend;
using Shared;

namespace Common.Network
{
    public interface INetworkEntityDestroyer
    {
        void Destroy(INetworkEntity entity);
    }

    public class NetworkEntityDestroyer : INetworkEntityDestroyer
    {
        public NetworkEntityDestroyer(INetworkConnection connection)
        {
            _connection = connection;
        }

        private readonly INetworkConnection _connection;

        public void Destroy(INetworkEntity entity)
        {
            var context = new EntityContexts.Destroy()
            {
                EntityId = entity.Id
            };

            _connection.OneWay(context);
        }
    }
}