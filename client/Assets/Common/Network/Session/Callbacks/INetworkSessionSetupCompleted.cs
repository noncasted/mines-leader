using Cysharp.Threading.Tasks;
using Internal;

namespace Network
{
    public interface INetworkSessionSetupCompleted : INetworkSessionCallbackEntry
    {
        UniTask OnSessionSetupCompleted(IReadOnlyLifetime lifetime);
    }
}