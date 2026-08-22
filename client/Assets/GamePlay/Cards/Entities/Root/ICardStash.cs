using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Cards
{
    public interface ICardStash
    {
        UniTask Enter(IReadOnlyLifetime lifetime);
    }
}
