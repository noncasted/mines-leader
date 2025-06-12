using Cysharp.Threading.Tasks;

namespace GamePlay.Players
{
    public interface IAvatarFactory
    {
        UniTask CreateLocal(PlayerBuildContext context);
        UniTask CreateRemote(PlayerBuildContext context);
    }
}