using Global.Backend;
using Internal;
using Shared;
using UnityEngine;

namespace Common.Network
{
    public class EntityPropertyUpdateCommand : OneWayCommand<SharedSessionObject.PropertyUpdate>
    {
        public EntityPropertyUpdateCommand(INetworkObjectsCollection objects)
        {
            _objects = objects;
        }

        private readonly INetworkObjectsCollection _objects;
        
        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionObject.PropertyUpdate context)
        {
            if (_objects.Entries.TryGetValue(context.ObjectId, out var networkObject) == false)
            {
                Debug.LogWarning("[Network] Received property update for unknown object ID: " + context.ObjectId);
                return;
            }
            
            var property = networkObject.Properties[context.PropertyId];
            property.Update(context.Value);
        }
    }
}