using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;

namespace GamePlay.Loop
{
    public interface IGameFlow
    {
        UniTask<GameResult> Execute(IReadOnlyLifetime lifetime);
        void OnLeave();
    }
}