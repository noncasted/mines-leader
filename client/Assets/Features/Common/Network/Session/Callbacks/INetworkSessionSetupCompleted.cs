using Cysharp.Threading.Tasks;
using Internal;

namespace Common.Network
{
    public interface INetworkSessionSetupCompleted : INetworkSessionCallbackEntry
    {
        UniTask OnSessionSetupCompleted(IReadOnlyLifetime lifetime);
    }
}