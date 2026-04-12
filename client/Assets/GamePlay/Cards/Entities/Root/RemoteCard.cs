using System;
using Cysharp.Threading.Tasks;
using GamePlay.Cards.Drop;
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
            ICardActionSync actionSync,
            IHand hand,
            ICardTransform transform,
            ICardRemoteDrop drop,
            ICardDefinition definition,
            ICardRevealView revealView)
        {
            Id = id;
            _view = view;
            _containerLifetime = containerLifetime;
            _actionSync = actionSync;
            Type = type;
            Hand = hand;
            Transform = transform;
            _drop = drop;
            Definition = definition;
            Lifetime = containerLifetime;
            _revealView = revealView;
        }

        private readonly ICardView _view;
        private readonly ILifetime _containerLifetime;
        private readonly ICardActionSync _actionSync;
        private readonly ICardRemoteDrop _drop;
        private readonly ICardRevealView _revealView;

        public Guid Id { get; }
        public CardType Type { get; }
        public ICardDefinition Definition { get; }
        public IHand Hand { get; }
        public ICardTransform Transform { get; }
        public IReadOnlyLifetime Lifetime { get; }
        public ICardRemoteDrop Drop => _drop;

        public void Reveal()
        {
            _revealView.Reveal();
        }

        public void PrepareForDrop()
        {
            _containerLifetime.Terminate();
        }

        public UniTask Use(IReadOnlyLifetime lifetime, ICardActionData data)
        {
            return _actionSync.Sync(lifetime, data);
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