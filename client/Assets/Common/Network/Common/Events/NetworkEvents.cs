using System;
using System.Collections.Generic;
using MemoryPack;
using Shared;

namespace Common.Network
{
    public class NetworkEvents : INetworkEvents
    {
        public NetworkEvents(INetworkSender sender, INetworkObject networkObject)
        {
            _sender = sender;
            _object = networkObject;
        }
        private readonly INetworkSender _sender;
        private readonly INetworkObject _object;

        private readonly Dictionary<Type, object> _entries = new();
        private readonly Dictionary<Type, Action<IEventPayload>> _actions = new();

        public IReadOnlyDictionary<Type, object> Entries => _entries;

        public void AddSource(Type type, object source, Action<IEventPayload> invoke)
        {
            _entries[type] = source;
            _actions[type] = invoke;
        }

        public void Invoke(byte[] rawPayload)
        {
            var payload = MemoryPackSerializer.Deserialize<IEventPayload>(rawPayload);
            var type = payload.GetType();
            
            if (_actions.TryGetValue(type, out var action) == false)
                throw new InvalidOperationException($"Event type {type} not found in actions.");
            
            action.Invoke(payload);
        }

        public void Send(IEventPayload rawPayload)
        {
            _sender.SendEmpty(new ObjectContexts.Event()
            {
                ObjectId = _object.Id,
                Value = MemoryPackSerializer.Serialize(rawPayload)
            });
        }
    }
}