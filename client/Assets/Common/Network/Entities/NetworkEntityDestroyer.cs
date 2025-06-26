using Cysharp.Threading.Tasks;
using Shared;

namespace Common.Network
{
    public interface INetworkEntityDestroyer
    {
        UniTask Destroy(INetworkEntity entity);
    }

    public class NetworkEntityDestroyer : INetworkEntityDestroyer
    {
        public NetworkEntityDestroyer(INetworkSender sender)
        {
            _sender = sender;
        }

        private readonly INetworkSender _sender;
        
        public async UniTask Destroy(INetworkEntity entity)
        {
            var context = new EntityContexts.Destroy()
            {
                EntityId = entity.Id
            };
            
            await _sender.SendEmpty(context);
        }
    }
}