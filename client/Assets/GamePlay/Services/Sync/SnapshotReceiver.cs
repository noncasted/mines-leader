using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Services
{
    public interface ISnapshotReceiver
    {
        void Add(Type type, Func<IMoveSnapshotRecord, UniTask> handler);
    }

    public class SnapshotReceiver : OneWayCommand<SharedMoveSnapshot>, ISnapshotReceiver, IReceivedSnapshots, IScopeSetup
    {
        public SnapshotReceiver(ILog<SnapshotReceiver> log)
        {
            _log = log;
        }

        private readonly ILog _log;
        private readonly Dictionary<Type, Func<IMoveSnapshotRecord, UniTask>> _handlers = new();
        private readonly EventSource<SharedMoveSnapshot> _received = new();
        private readonly Queue<IMoveSnapshotRecord> _queue = new();

        private Guid _roundPlayerId;

        public IEventSource<SharedMoveSnapshot> Received => _received;

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
            _received.Invoke(context);

            foreach (var record in context.Records)
            {
                if (record is SharedBoardSnapshot boardSnapshot)
                {
                    ProcessBoardSnapshot(boardSnapshot);
                    continue;
                }

                if (record is TimeLimitedRoundRecord timeLimited)
                {
                    DispatchRound(record, timeLimited.CurrentPlayer);
                    continue;
                }

                if (record is LastManStandingRoundRecord lastManStanding)
                {
                    DispatchRound(record, lastManStanding.CurrentPlayer);
                    continue;
                }

                // Ресурсы игрока идут через очередь наравне со всем остальным: сервер
                // присылает их после записи о действии, и счётчики обязаны меняться
                // только когда анимация карты или вскрытия уже отыграла.
                Enqueue(record);
            }
        }

        private void DispatchRound(IMoveSnapshotRecord record, Guid currentPlayer)
        {
            if (currentPlayer == _roundPlayerId)
            {
                HandleRecordImmediately(record);
                return;
            }

            // Player switches wait for drop/stash visuals so the pile is not reset mid-turn.
            Enqueue(record);
        }

        private void Enqueue(IMoveSnapshotRecord record)
        {
            _queue.Enqueue(record);
        }

        private void HandleRecordImmediately(IMoveSnapshotRecord record)
        {
            if (_handlers.TryGetValue(record.GetType(), out var handler) == false)
                return;

            handler.Invoke(record);
            RememberRoundPlayer(record);
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
                Enqueue(new SharedBoardSnapshot
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
                        RememberRoundPlayer(record);
                    }
                }
                catch (OperationCanceledException) when (lifetime.IsTerminated == true)
                {
                    // The match scope is torn down while a record is shown (a card flies): its waits are canceled.
                    return;
                }
                catch (Exception e)
                {
                    _log.Error(e, "Exception occurred while processing move snapshot records");
                }
            }
        }

        private void RememberRoundPlayer(IMoveSnapshotRecord record)
        {
            switch (record)
            {
                case TimeLimitedRoundRecord timeLimited:
                    _roundPlayerId = timeLimited.CurrentPlayer;
                    break;
                case LastManStandingRoundRecord lastManStanding:
                    _roundPlayerId = lastManStanding.CurrentPlayer;
                    break;
            }
        }
    }
}