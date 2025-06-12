using Cysharp.Threading.Tasks;
using Internal;

namespace Common.Network
{
    public interface INetworkCommandsDispatcher
    {
        UniTask Run(IReadOnlyLifetime lifetime);
    }
}