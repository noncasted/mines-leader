using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.Backend;
using Global.Systems;
using Internal;
using Shared;

namespace Common.Network
{
    public class NetworkPropertiesCollector : IUpdatable, IScopeSetup
    {
        public NetworkPropertiesCollector(
            INetworkSocket socket,
            IUpdater updater,
            INetworkObjectsCollection objects)
        {
            _socket = socket;
            _updater = updater;
            _objects = objects;
        }

        private readonly INetworkSocket _socket;
        private readonly IUpdater _updater;
        private readonly INetworkObjectsCollection _objects;

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

            var contexts = new List<ObjectContexts.SetProperty>();
            
            foreach (var (_, networkObject) in _objects.Entries)
            {
                foreach (var (id, property) in networkObject.Properties)
                {
                    if (property.IsDirty == false)
                        continue;

                    contexts.Add(new ObjectContexts.SetProperty()
                    {
                        ObjectId = networkObject.Id,
                        PropertyId = id,
                        Value = property.Collect()
                    });
                }
            }
            
            Send(contexts).Forget();
        }

        private async UniTask Send(IReadOnlyList<ObjectContexts.SetProperty> requests)
        {
            foreach (var request in requests)
                await _socket.Send(request);
        }
    }
}