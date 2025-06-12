using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Cards
{
    public interface ICardAction
    {
        UniTask<bool> Execute(IReadOnlyLifetime lifetime);
    }
}