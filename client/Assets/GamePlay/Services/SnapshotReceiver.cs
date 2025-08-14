using System;
using System.Collections.Generic;
using Global.Backend;
using Internal;
using Shared;

namespace GamePlay.Services
{
    public interface ISnapshotReceiver
    {
        void Add(Type type, Action<IMoveSnapshotRecord> handler);
    }
    
    public class SnapshotReceiver : OneWayCommand<SharedMoveSnapshot>, ISnapshotReceiver
    {
        private readonly Dictionary<Type, Action<IMoveSnapshotRecord>> _handlers = new();

        public void Add(Type type, Action<IMoveSnapshotRecord> handler)
        {
            _handlers[type] = handler;
        }
        
        protected override void Execute(IReadOnlyLifetime lifetime, SharedMoveSnapshot context)
        {
            foreach (var record in context.Records)
            {
                var type = record.GetType();

                if (_handlers.TryGetValue(type, out var handler) == false)
                    throw new ArgumentException($"No handler found for record type {type.Name}.", nameof(context));

                handler.Invoke(record);
            }
        }
    }
}