using Common.Network;
using Internal;
using MemoryPack;

namespace GamePlay.Cards
{
    public interface ICardUseSync
    {
        void Send(ICardUseEvent payload);
    }
    
    public class CardUseSyncSender : ICardUseSync
    {
        public CardUseSyncSender(INetworkEntity entity)
        {
            _entity = entity;
        }

        private readonly INetworkEntity _entity;

        public void Send(ICardUseEvent payload)
        {
            _entity.Events.Send(new CardUseSyncPayload()
            {
                Payload = payload
            });
        }
    }
    
    public class CardUseSyncReceiver : IScopeSetup
    {
        public CardUseSyncReceiver(INetworkEntity entity, ICardActionSync action)
        {
            _entity = entity;
            _action = action;
        }

        private readonly INetworkEntity _entity;
        private readonly ICardActionSync _action;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _entity.Events.GetEvent<CardUseSyncPayload>().Advise(lifetime, OnReceived);
        }
        
        private void OnReceived(CardUseSyncPayload payload)
        {
            _action.ShowOnRemote(_entity.Lifetime, payload.Payload);
        }
    }

    [MemoryPackable]
    public partial class CardUseSyncPayload : IEventPayload
    {
        public ICardUseEvent Payload { get; set; }
    }
}