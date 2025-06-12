using Cysharp.Threading.Tasks;
using GamePlay.Players;

namespace GamePlay.Cards
{
    public interface IStashFactory
    {
        UniTask CreateLocal(PlayerBuildContext context);
        UniTask CreateRemote(PlayerBuildContext context);
    }
}