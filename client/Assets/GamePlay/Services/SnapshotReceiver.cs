using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;
using Shared;

namespace GamePlay.Services
{
    public interface ISnapshotReceiver
    {
        void Add(Type type, Func<IMoveSnapshotRecord, UniTask> handler);
    }

    public class SnapshotReceiver : OneWayCommand<SharedMoveSnapshot>, ISnapshotReceiver, IScopeSetup
    {
        private readonly Dictionary<Type, Func<IMoveSnapshotRecord, UniTask>> _handlers = new();
        private readonly Queue<IMoveSnapshotRecord> _queue = new();

        public void Add(Type type, Func<IMoveSnapshotRecord, UniTask> handler)
        {
            _handlers[type] = handler;
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            Loop(lifetime).Forget();
        }

        protected override void Execute(IReadOnlyLifetime lifetime, SharedMoveSnapshot context)
        {
            foreach (var record in context.Records)
                _queue.Enqueue(record);
        }

        private async UniTask Loop(IReadOnlyLifetime lifetime)
        {
            while (lifetime.IsTerminated == false)
            {
                if (_queue.Count == 0)
                    await UniTask.Yield();

                while (_queue.TryPeek(out var record) == true)
                {
                    var type = record.GetType();

                    if (_handlers.TryGetValue(type, out var handler) == false)
                        throw new ArgumentException($"No handler found for record type {type.Name}.");

                    await handler.Invoke(record);
                }
            }
        }
    }
}