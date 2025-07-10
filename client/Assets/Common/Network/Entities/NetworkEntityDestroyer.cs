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
        public NetworkEntityDestroyer(INetworkSocket socket)
        {
            _socket = socket;
        }

        private readonly INetworkSocket _socket;
        
        public async UniTask Destroy(INetworkEntity entity)
        {
            var context = new EntityContexts.Destroy()
            {
                EntityId = entity.Id
            };
            
            await _socket.Send(context);
        }
    }
}