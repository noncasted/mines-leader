using Global.Backend;
using Internal;
using Shared;

namespace Common.Network
{
    public class EntityEventCommand : OneWayCommand<SharedSessionObject.Event>
    {
        public EntityEventCommand(INetworkObjectsCollection objects)
        {
            _objects = objects;
        }

        private readonly INetworkObjectsCollection _objects;

        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionObject.Event context)
        {
            var networkObject = _objects.Entries[context.ObjectId];
            networkObject.Events.Invoke(context.Value);
        }
    }
}