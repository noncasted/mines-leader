using Cysharp.Threading.Tasks;
using Global.Backend;
using Shared;

namespace Common.Network
{
    public interface INetworkEntityDestroyer
    {
        UniTask Destroy(INetworkEntity entity);
    }

    public class NetworkEntityDestroyer : INetworkEntityDestroyer
    {
        public NetworkEntityDestroyer(INetworkConnection connection)
        {
            _connection = connection;
        }

        private readonly INetworkConnection _connection;
        
        public async UniTask Destroy(INetworkEntity entity)
        {
            var context = new EntityContexts.Destroy()
            {
                EntityId = entity.Id
            };
            
            await _connection.Send(context);
        }
    }
}