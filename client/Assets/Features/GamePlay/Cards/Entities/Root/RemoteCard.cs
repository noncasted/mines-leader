using Common.Network;
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
            IHand hand,
            ICardTransform transform, 
            ICardDefinition definition)
        {
            _entity = entity;
            _view = view;
            _containerLifetime = containerLifetime;
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
    }
}