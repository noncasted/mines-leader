using Global.Backend;
using Internal;
using Shared;

namespace Common.Network
{
    public class EntityPropertyUpdateCommand : OneWayCommand<ObjectContexts.PropertyUpdate>
    {
        public EntityPropertyUpdateCommand(INetworkObjectsCollection objects)
        {
            _objects = objects;
        }

        private readonly INetworkObjectsCollection _objects;
        
        protected override void Execute(IReadOnlyLifetime lifetime, ObjectContexts.PropertyUpdate context)
        {
            var networkObject = _objects.Entries[context.ObjectId];
            var property = networkObject.Properties[context.PropertyId];
            property.Update(context.Value);
        }
    }
}