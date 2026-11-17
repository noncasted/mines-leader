using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Loop;
using GamePlay.Players;
using Internal;
using Shared;

namespace GamePlay.Metrics.Tests
{
    // The scope of a match for the writers: a fake stream, the snapshots pushed by the test, the local player and the
    // other one. Dispose ends the scope: Ending, then the end of the stream.
    public class MetricsTestKit : IDisposable
    {
        public MetricsTestKit()
        {
            Context = new TestContext(new TestPlayer(Self), new TestPlayer(Other));
            Session = new MatchMetricsSession(Metrics, Clock, Guid.NewGuid());
            Session.OnSetup(_lifetime);
        }

        private readonly Lifetime _lifetime = new();

        public readonly Guid Self = Guid.NewGuid();
        public readonly Guid Other = Guid.NewGuid();
        public readonly FakeMatchMetrics Metrics = new();
        public readonly MatchClock Clock = new();
        public readonly TestSnapshots Snapshots = new();
        public readonly MatchMetricsSession Session;

        public IReadOnlyLifetime Lifetime => _lifetime;
        public readonly IGameContext Context;

        public void Push(params IMoveSnapshotRecord[] records)
        {
            Snapshots.Push(new SharedMoveSnapshot { Records = records });
        }

        public void Dispose()
        {
            _lifetime.Terminate();
        }

        public static SharedBoardSnapshot Board(Guid owner, params IBoardSnapshotRecord[] records)
        {
            return new SharedBoardSnapshot { BoardOwnerId = owner, Records = new List<IBoardSnapshotRecord>(records) };
        }

        public static BoardSnapshotRecord.CellFree Free(int x, int y)
        {
            return new BoardSnapshotRecord.CellFree { Position = new Position(x, y) };
        }

        public static BoardSnapshotRecord.MinesAround Mines(int x, int y, int count)
        {
            return new BoardSnapshotRecord.MinesAround { Position = new Position(x, y), Count = count };
        }

        public class TestSnapshots : IMatchSnapshots
        {
            private readonly EventSource<SharedMoveSnapshot> _received = new();

            public IEventSource<SharedMoveSnapshot> Received => _received;

            public void Push(SharedMoveSnapshot snapshot) => _received.Invoke(snapshot);
        }

        public class TestPlayer : IGamePlayer
        {
            public TestPlayer(Guid id)
            {
                Id = id;
            }

            public Guid Id { get; }
            public IGamePlayerInfo Info => throw new NotSupportedException();
            public IContainer Scope => throw new NotSupportedException();
            public IPlayerMana Mana => throw new NotSupportedException();
            public IPlayerHealth Health => throw new NotSupportedException();
            public IPlayerTurns Turns => throw new NotSupportedException();
            public IHand Hand => throw new NotSupportedException();
            public ICardTable Table => throw new NotSupportedException();
            public IBoard Board => throw new NotSupportedException();
            public IPlayerModifiers Modifiers => throw new NotSupportedException();
        }

        public class TestContext : IGameContext
        {
            public TestContext(IGamePlayer self, IGamePlayer other)
            {
                Self = self;
                Other = other;
                All = new[] { self, other };
            }

            public IGamePlayer Self { get; }
            public IGamePlayer Other { get; }
            public IReadOnlyList<IGamePlayer> All { get; }
            public bool IsGameStarted => true;
            public bool IsPaused => false;
            public int CardMovesCost => 1;

            public void AddPlayer(IGamePlayer player) => throw new NotSupportedException();
            public void SetGameStarted(int cardMovesCost) => throw new NotSupportedException();
            public void SetPaused(bool isPaused) => throw new NotSupportedException();
        }

        public class TestGameState : IGameState
        {
            private readonly ViewableProperty<GameStateType> _value = new(GameStateType.Active);
            private readonly ViewableProperty<MatchCompletedData> _completed = new();

            public IViewableProperty<GameStateType> Value => _value;
            public IViewableProperty<MatchCompletedData> CompletedData => _completed;

            public void Complete(MatchResultType type) => _completed.Set(new MatchCompletedData { Type = type });

            public void Set(GameStateType type) => throw new NotSupportedException();
            public void SetWinner(GameCompletedRecord record) => throw new NotSupportedException();
            public UniTask<MatchCompletedData> WaitCompletion(IReadOnlyLifetime lifetime) => throw new NotSupportedException();
            public void OnLeave() => throw new NotSupportedException();
        }
    }
}
