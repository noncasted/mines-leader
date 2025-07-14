using System;
using System.Collections.Generic;
using Global.Backend;
using Internal;
using MemoryPack;
using Shared;

namespace Common.Network
{
    [MemoryPackable(GenerateType.NoGenerate)]
    public partial interface IEventPayload
    {
        
    }
    
    public interface INetworkEvents
    {
        IReadOnlyDictionary<Type, object> Entries { get; }

        void AddSource(Type type, object source, Action<IEventPayload> invoke);
        void Invoke(byte[] rawPayload);
        void Send(IEventPayload rawPayload);
    }
    
    public class NetworkEvents : INetworkEvents
    {
        public NetworkEvents(INetworkConnection connection, INetworkObject networkObject)
        {
            _connection = connection;
            _object = networkObject;
        }
        private readonly INetworkConnection _connection;
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
            _connection.OneWay(new ObjectContexts.Event()
            {
                ObjectId = _object.Id,
                Value = MemoryPackSerializer.Serialize(rawPayload)
            });
        }
    }
    
    public static class NetworkEventsExtensions
    {
        public static IViewableDelegate<T> GetEvent<T>(this INetworkEvents events) where T : IEventPayload
        {
            var type = typeof(T);

            if (events.Entries.ContainsKey(type) == false)
            {
                var source = new ViewableDelegate<T>();
                
                events.AddSource(type, source, payload =>
                {
                    if (payload is not T castedPayload)
                        throw new InvalidCastException();
                    
                    source.Invoke(castedPayload);
                });
            }

            return events.Entries[type] as ViewableDelegate<T>;
        }
    }
}