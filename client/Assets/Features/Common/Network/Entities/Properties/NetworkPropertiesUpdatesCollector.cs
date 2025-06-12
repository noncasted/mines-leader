using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using Shared;

namespace Common.Network
{
    public class NetworkPropertiesUpdatesCollector : IUpdatable, IScopeSetup
    {
        public NetworkPropertiesUpdatesCollector(
            IUpdater updater,
            INetworkEntitiesCollection entities,
            INetworkSender sender)
        {
            _updater = updater;
            _entities = entities;
            _sender = sender;
        }

        private readonly IUpdater _updater;
        private readonly INetworkEntitiesCollection _entities;
        private readonly INetworkSender _sender;

        private float _updateInterval = 0.1f;
        private float _timer = 0f;
        
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
            _timer += delta;
            
            if (_timer < _updateInterval)
                return;
            
            _timer = 0f;

            var contexts = new List<EntityContexts.UpdatePropertyRequest>();
            
            foreach (var (_, entity) in _entities.Entries)
            {
                for (var i = 0; i < entity.Properties.Count; i++)
                {
                    var property = entity.Properties[i];
                    
                    if (property.IsDirty == false)
                        continue;

                    contexts.Add(new EntityContexts.UpdatePropertyRequest()
                    {
                        EntityId = entity.Id,
                        PropertyId = i,
                        Value = property.Collect()
                    });
                }
            }
            
            Send(contexts).Forget();
        }

        private async UniTask Send(IReadOnlyList<EntityContexts.UpdatePropertyRequest> requests)
        {
            foreach (var request in requests)
            {
                await _sender.SendEmpty(request);
            }
        }
    }
}