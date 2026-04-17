using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Network;
using Shared;
using UnityEngine;

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
            {
                if (record is SharedBoardSnapshot boardSnapshot)
                {
                    ProcessBoardSnapshot(boardSnapshot);
                    continue;
                }

                if (record is TimeLimitedRoundRecord || record is LastManStandingRoundRecord)
                {
                    HandleRecordImmediately(record);
                    continue;
                }

                _queue.Enqueue(record);
            }
        }

        private void HandleRecordImmediately(IMoveSnapshotRecord record)
        {
            if (_handlers.TryGetValue(record.GetType(), out var handler) == false)
                return;

            handler.Invoke(record);
        }

        private void ProcessBoardSnapshot(SharedBoardSnapshot boardSnapshot)
        {
            List<IBoardSnapshotRecord> queued = null;

            foreach (var record in boardSnapshot.Records)
            {
                if (record is BoardSnapshotRecord.Flag)
                {
                    HandleBoardRecordImmediately(boardSnapshot.BoardOwnerId, record);
                    continue;
                }

                queued ??= new List<IBoardSnapshotRecord>();
                queued.Add(record);
            }

            if (queued != null)
            {
                _queue.Enqueue(new SharedBoardSnapshot
                {
                    BoardOwnerId = boardSnapshot.BoardOwnerId,
                    Records = queued
                });
            }
        }

        private void HandleBoardRecordImmediately(Guid boardOwnerId, IBoardSnapshotRecord record)
        {
            if (_handlers.TryGetValue(typeof(SharedBoardSnapshot), out var handler) == false)
                return;

            var snapshot = new SharedBoardSnapshot
            {
                BoardOwnerId = boardOwnerId,
                Records = new List<IBoardSnapshotRecord> { record }
            };

            handler.Invoke(snapshot);
        }

        private async UniTask Loop(IReadOnlyLifetime lifetime)
        {
            while (lifetime.IsTerminated == false)
            {
                if (_queue.Count == 0)
                    await UniTask.Yield();

                try
                {
                    while (_queue.TryDequeue(out var record) == true)
                    {
                        var type = record.GetType();

                        if (_handlers.TryGetValue(type, out var handler) == false)
                            throw new ArgumentException($"No handler found for record type {type.Name}.");

                        await handler.Invoke(record);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception occurred while processing move snapshot records: {e}");
                }
            }
        }
    }
}