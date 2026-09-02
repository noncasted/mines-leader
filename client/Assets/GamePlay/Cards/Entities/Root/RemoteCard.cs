using System;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class RemoteCard : IRemoteCard
    {
        public RemoteCard(
            Guid id,
            ICardView view,
            ILifetime containerLifetime,
            CardType type,
            IHand hand,
            ICardTransform transform,
            ICardRemoteDrop drop,
            ICardStash stash,
            ICardDropped dropped,
            ICardDefinition definition,
            ICardRevealView revealView)
        {
            Id = id;
            _view = view;
            _containerLifetime = containerLifetime;
            Type = type;
            Hand = hand;
            Transform = transform;
            _drop = drop;
            _stash = stash;
            _dropped = dropped;
            Definition = definition;
            Lifetime = containerLifetime;
            _revealView = revealView;
        }

        private readonly ICardView _view;
        private readonly ILifetime _containerLifetime;
        private readonly ICardRemoteDrop _drop;
        private readonly ICardStash _stash;
        private readonly ICardDropped _dropped;
        private readonly ICardRevealView _revealView;

        public Guid Id { get; }
        public CardType Type { get; }
        public ICardDefinition Definition { get; }
        public IHand Hand { get; }
        public ICardTransform Transform { get; }
        public IReadOnlyLifetime Lifetime { get; }
        public ICardRemoteDrop Drop => _drop;
        public ICardStash Stash => _stash;
        public ICardDropped Dropped => _dropped;

        public void Reveal()
        {
            _revealView.Reveal();
        }

        public void PrepareForDrop()
        {
            _containerLifetime.Terminate();
        }

        public UniTask Destroy()
        {
            Debug.Log($"[Game] [Card] Destroying remote card with ID: {Id}");
            _containerLifetime.Terminate();
            _view.Destroy();

            return UniTask.CompletedTask;
        }
    }
}