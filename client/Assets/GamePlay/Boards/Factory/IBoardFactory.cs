using Cysharp.Threading.Tasks;
using GamePlay.Players;

namespace GamePlay.Boards
{
    public interface IBoardFactory
    {
        UniTask CreateLocal(PlayerBuildContext context);
        UniTask CreateRemote(PlayerBuildContext context);
    }
}