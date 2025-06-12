using Cysharp.Threading.Tasks;
using GamePlay.Players;

namespace GamePlay.Cards
{
    public interface IHandFactory
    {
        UniTask Create(PlayerBuildContext context);
    }
}