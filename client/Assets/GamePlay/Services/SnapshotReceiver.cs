using System;
using System.Collections.Generic;
using System.Linq;
using Global.Backend;
using Internal;
using Shared;

namespace GamePlay.Services
{
    public class SnapshotReceiver : OneWayCommand<SharedMoveSnapshot>
    {
        public SnapshotReceiver(IReadOnlyList<ISnapshotHandler> handlers)
        {
            _handlers = handlers.ToDictionary(t => t.Target);
        }

        private readonly IReadOnlyDictionary<Type, ISnapshotHandler> _handlers;

        protected override void Execute(IReadOnlyLifetime lifetime, SharedMoveSnapshot context)
        {
            foreach (var record in context.Records)
            {
                var type = record.GetType();

                if (_handlers.TryGetValue(type, out var handler) == false)
                    throw new ArgumentException($"No handler found for record type {type.Name}.", nameof(context));

                handler.Handle(record);
            }
        }
    }
}