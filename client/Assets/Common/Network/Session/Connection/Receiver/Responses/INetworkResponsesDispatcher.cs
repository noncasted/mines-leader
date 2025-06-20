using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public interface INetworkResponsesDispatcher
    {
        UniTask Run(IReadOnlyLifetime lifetime);
        UniTask<T> AwaitResponse<T>(ServerFullRequest request);
    }
}