using Cysharp.Threading.Tasks;

namespace GamePlay.Cards
{
    public interface ICardLocalDrag
    {
        UniTask Enter(ICardLocalIdle idle);
    }
}