using Cysharp.Threading.Tasks;
using Shared;

namespace Global.Backend
{
    public interface IBackendProjection<T> where T : INetworkContext
    {
        UniTask OnReceived(T data);
    }
}