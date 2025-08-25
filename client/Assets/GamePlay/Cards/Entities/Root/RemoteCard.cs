using Common.Network;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    public class RemoteCard : IScopeSetup, IRemoteCard
    {
        public RemoteCard(
            INetworkEntity entity,
            ICardView view,
            ILifetime containerLifetime,
            CardType type,
            CardTarget target,
            ICardActionSync actionSync,
            IHand hand,
            ICardTransform transform, 
            ICardDefinition definition)
        {
            _entity = entity;
            _view = view;
            _containerLifetime = containerLifetime;
            _actionSync = actionSync;
            Type = type;
            Target = target;
            Hand = hand;
            Transform = transform;
            Definition = definition;
            Lifetime = entity.Lifetime;
        }
        
        private readonly INetworkEntity _entity;
        private readonly ICardView _view;
        private readonly ILifetime _containerLifetime;
        private readonly ICardActionSync _actionSync;

        public int EntityId => _entity.Id;
        public CardType Type { get; }
        public CardTarget Target { get; }
        public ICardDefinition Definition { get; }
        public IHand Hand { get; }
        public ICardTransform Transform { get; }
        public IReadOnlyLifetime Lifetime { get; }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _entity.Lifetime.Listen(() =>
            {
                _containerLifetime.Terminate();
                _view.Destroy();
            });    
        }
        
        public UniTask Use(IReadOnlyLifetime lifetime, ICardActionData data)
        {
            return _actionSync.Sync(lifetime, data);
        }
    }
}