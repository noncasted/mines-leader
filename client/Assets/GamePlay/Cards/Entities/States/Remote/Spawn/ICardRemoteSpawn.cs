using Cysharp.Threading.Tasks;

namespace GamePlay.Cards
{
    public interface ICardRemoteSpawn
    {
        UniTask Execute();
    }
}