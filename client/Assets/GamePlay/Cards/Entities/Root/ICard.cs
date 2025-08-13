using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    public interface ICard
    {
        int EntityId { get; }
        CardType Type { get; }
        CardTarget Target { get; }
        ICardDefinition Definition { get; }
        IHand Hand { get; }
        ICardTransform Transform { get; }
        IReadOnlyLifetime Lifetime { get; }

        void Use(IReadOnlyLifetime lifetime);
    }
}