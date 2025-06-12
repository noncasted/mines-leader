using Cysharp.Threading.Tasks;
using Internal;

namespace Common.Network
{
    public interface INetworkServiceEntityFactory
    {
        UniTask<INetworkEntity> Create(
            IReadOnlyLifetime lifetime,
            string key,
            params INetworkProperty[] properties);
    }
}