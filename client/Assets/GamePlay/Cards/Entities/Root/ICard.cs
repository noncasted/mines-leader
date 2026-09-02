using System;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    public interface ICard
    {
        Guid Id { get; }
        CardType Type { get; }
        ICardDefinition Definition { get; }
        IHand Hand { get; }
        ICardTransform Transform { get; }
        IReadOnlyLifetime Lifetime { get; }
        ICardStash Stash { get; }
        ICardDropped Dropped { get; }

        UniTask Destroy();
    }
}