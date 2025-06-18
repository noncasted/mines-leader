using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public class EntityEventCommand : NetworkCommand<ObjectContexts.Event>
    {
        public EntityEventCommand(INetworkObjectsCollection objects)
        {
            _objects = objects;
        }

        private readonly INetworkObjectsCollection _objects;

        protected override UniTask Execute(IReadOnlyLifetime lifetime, ObjectContexts.Event context)
        {
            var networkObject = _objects.Entries[context.ObjectId];
            networkObject.Events.Invoke(context.Value);
            return UniTask.CompletedTask;
        }
    }
}