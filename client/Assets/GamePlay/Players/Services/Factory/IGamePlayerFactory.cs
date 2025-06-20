using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Players
{
    public interface IGamePlayerFactory
    {
        UniTask<IGamePlayer> CreateLocal(IReadOnlyLifetime lifetime);
        UniTask<IReadOnlyList<IGamePlayer>> WaitRemote(IReadOnlyLifetime lifetime, int remoteAmount);
    }
}