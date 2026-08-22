using System;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class LocalCard : ILocalCard
    {
        public LocalCard(
            Guid id,
            CardType type,
            ILifetime containerLifetime,
            ICardActionSync actionSync,
            IHand hand,
            ICardTransform transform,
            ICardLocalDrop drop,
            ICardStash stash,
            ICardView view,
            ICardDefinition definition,
            ICardPointerHandler pointerHandler)
        {
            Id = id;
            Type = type;
            _containerLifetime = containerLifetime;
            _actionSync = actionSync;
            _drop = drop;
            _stash = stash;
            _view = view;
            Definition = definition;
            Hand = hand;
            Transform = transform;
            Lifetime = containerLifetime;
            PointerHandler = pointerHandler;
        }

        private readonly ILifetime _containerLifetime;
        private readonly ICardActionSync _actionSync;
        private readonly ICardLocalDrop _drop;
        private readonly ICardStash _stash;
        private readonly ICardView _view;
        private readonly ViewableDelegate _used = new();
        private readonly ViewableProperty<bool> _isInSpawnAnimation = new();

        public Guid Id { get; }
        public CardType Type { get; }
        public ICardDefinition Definition { get; }
        public IHand Hand { get; }
        public ICardTransform Transform { get; }
        public IReadOnlyLifetime Lifetime { get; }

        public IViewableDelegate Used => _used;
        public ICardLocalDrop Drop => _drop;
        public ICardStash Stash => _stash;
        public ICardPointerHandler PointerHandler { get; }
        public IViewableProperty<bool> IsInSpawnAnimation => _isInSpawnAnimation;

        public void SetSpawning(bool isSpawning)
        {
            _isInSpawnAnimation.Set(isSpawning);
        }

        public UniTask Destroy()
        {
            Debug.Log($"[Game] [Card] Destroying local card with ID: {Id}");
            _containerLifetime.Terminate();
            _view.Destroy();

            return UniTask.CompletedTask;
        }

        public UniTask Use(IReadOnlyLifetime lifetime, ICardActionData data)
        {
            return _actionSync.Sync(lifetime, data);
        }
    }
}