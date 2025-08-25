using Common.Network;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    public class LocalCard : ILocalCard
    {
        public LocalCard(
            CardType type,
            CardTarget target,
            ILifetime containerLifetime,
            ICardActionSync actionSync,
            IHand hand,
            ICardTransform transform,
            INetworkEntity entity,
            ICardView view,
            ICardDefinition definition)
        {
            Type = type;
            Target = target;
            _containerLifetime = containerLifetime;
            _actionSync = actionSync;
            _entity = entity;
            _view = view;
            Definition = definition;
            Hand = hand;
            Transform = transform;
            Lifetime = entity.Lifetime;
        }

        private readonly ILifetime _containerLifetime;
        private readonly ICardActionSync _actionSync;
        private readonly INetworkEntity _entity;
        private readonly ICardView _view;
        private readonly ViewableDelegate _used = new();

        public int EntityId => _entity.Id;
        public CardType Type { get; }
        public CardTarget Target { get; }
        public ICardDefinition Definition { get; }
        public IHand Hand { get; }
        public ICardTransform Transform { get; }
        public IReadOnlyLifetime Lifetime { get; }

        public IViewableDelegate Used => _used;

        public UniTask Destroy()
        {
            _containerLifetime.Terminate();
            _entity.Destroy();
            _view.Destroy();

            return UniTask.CompletedTask;
        }

        public UniTask Use(IReadOnlyLifetime lifetime, ICardActionData data)
        {
            return _actionSync.Sync(lifetime, data);
        }
    }
}