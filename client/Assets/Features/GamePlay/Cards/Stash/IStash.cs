using Cysharp.Threading.Tasks;
using Global.GameServices;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public interface IStash
    {
        bool IsEmpty { get; }
        void AddCard(CardType type);
        UniTask DrawCard(IReadOnlyLifetime lifetime);
    }
}