using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Shared;

namespace GamePlay.Services
{
    public interface ISnapshotReceiver
    {
        void Add(Type type, Func<IMoveSnapshotRecord, UniTask> handler);
    }

    // Applies the records of the move snapshots: most of them one after another through a queue (a record waits for the
    // animation of the one before it), a few at once. A handler that throws breaks the synced state: the match ends
    // with an error (IGameState.OnFailed), the error goes to the log and with it to the client telemetry.
    public class SnapshotReceiver : OneWayCommand<SharedMoveSnapshot>, ISnapshotReceiver, IReceivedSnapshots, IScopeSetup
    {
        public SnapshotReceiver(IGameState gameState, ILog<SnapshotReceiver> log)
        {
            _gameState = gameState;
            _log = log;
        }

        private readonly IGameState _gameState;
        private readonly ILog _log;
        private readonly Dictionary<Type, Func<IMoveSnapshotRecord, UniTask>> _handlers = new();
        private readonly EventSource<SharedMoveSnapshot> _received = new();
        private readonly Queue<IMoveSnapshotRecord> _queue = new();
        private readonly EventSource _enqueued = new();

        private Guid _roundPlayerId;
        private bool _isFailed;

        public IEventSource<SharedMoveSnapshot> Received => _received;

        public void Add(Type type, Func<IMoveSnapshotRecord, UniTask> handler)
        {
            _handlers[type] = handler;
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            Loop(lifetime).NoAwait();
        }

        protected override void Execute(IReadOnlyLifetime lifetime, SharedMoveSnapshot context)
        {
            // The match is ending with an error: the snapshots after it are not applied to the broken state.
            if (_isFailed == true)
                return;

            _received.Invoke(context);

            // The boundary of the records applied at once: their failure ends the match as one in the queue does,
            // instead of the log of the network dispatcher and a match that goes on out of sync.
            try
            {
                Dispatch(context);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void Dispatch(SharedMoveSnapshot context)
        {
            foreach (var record in context.Records)
            {
                if (record is SharedBoardSnapshot boardSnapshot)
                {
                    DispatchBoardSnapshot(boardSnapshot);
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

                // The resources of a player go through the queue with everything else: the server sends them after the
                // record of the action, and the counters change only when the card or the reveal animation has played.
                Enqueue(record);
            }
        }

        private void DispatchRound(IMoveSnapshotRecord record, Guid currentPlayer)
        {
            if (currentPlayer == _roundPlayerId)
            {
                HandleNow(record);
                RememberRoundPlayer(record);
                return;
            }

            // Player switches wait for drop/stash visuals so the pile is not reset mid-turn.
            Enqueue(record);
        }

        private void DispatchBoardSnapshot(SharedBoardSnapshot boardSnapshot)
        {
            List<IBoardSnapshotRecord> queued = null;

            foreach (var record in boardSnapshot.Records)
            {
                if (record is BoardSnapshotRecord.Flag)
                {
                    HandleNow(new SharedBoardSnapshot
                    {
                        BoardOwnerId = boardSnapshot.BoardOwnerId,
                        Records = new List<IBoardSnapshotRecord> { record }
                    });

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

        // A record applied at once has a synchronous handler (Resolver.Handle returns the task of BoardSnapshotHandler or
        // of a round handler, both complete): a pending one would apply after the records queued behind it and lose its
        // exception, so it is an error, and a faulted one rethrows here.
        private void HandleNow(IMoveSnapshotRecord record)
        {
            var handling = HandlerOf(record).Invoke(record);

            if (handling.Status == UniTaskStatus.Pending)
            {
                throw new InvalidOperationException(
                    $"[Snapshot] The handler of {record.GetType().Name} is applied at once and must complete synchronously.");
            }

            handling.GetAwaiter().GetResult();
        }

        private void Enqueue(IMoveSnapshotRecord record)
        {
            _queue.Enqueue(record);
            _enqueued.Invoke();
        }

        private async UniTask Loop(IReadOnlyLifetime lifetime)
        {
            try
            {
                while (lifetime.IsTerminated == false && _isFailed == false)
                {
                    if (_queue.Count == 0)
                        await WaitEnqueued(lifetime);

                    var record = _queue.Dequeue();

                    await HandlerOf(record).Invoke(record);
                    RememberRoundPlayer(record);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsTerminated == true)
            {
                // The match scope is torn down while the queue is empty or a record is shown (a card flies): its waits
                // are canceled, the records left in the queue are not applied.
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // The wake comes inside Execute: the queue starts on the next player loop, after every record of the received
        // snapshot is dispatched, so the records applied at once come before the queued ones of the same snapshot.
        private async UniTask WaitEnqueued(IReadOnlyLifetime lifetime)
        {
            await _enqueued.WaitInvoke(lifetime);
            await UniTask.Yield(lifetime.Token);
        }

        private Func<IMoveSnapshotRecord, UniTask> HandlerOf(IMoveSnapshotRecord record)
        {
            var type = record.GetType();

            if (_handlers.TryGetValue(type, out var handler) == false)
                throw new ArgumentException($"[Snapshot] No handler for the record type {type.Name}.");

            return handler;
        }

        private void Fail(Exception exception)
        {
            _isFailed = true;
            _log.Error(exception, "[Snapshot] A record failed to apply: the match ends with an error");
            _gameState.OnFailed();
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
