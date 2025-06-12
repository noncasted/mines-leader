using Cysharp.Threading.Tasks;

namespace Common.Network
{
    public interface INetworkEntityDestroyer
    {
        UniTask Destroy(INetworkEntity entity);
    }
}