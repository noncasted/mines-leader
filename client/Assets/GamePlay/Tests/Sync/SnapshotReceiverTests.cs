using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Services;
using Internal;
using NUnit.Framework;
using Shared;
using UnityEngine.TestTools;

namespace GamePlay.Tests
{
    // The queue of SnapshotReceiver: board flags at once, the rest of the board and the resources of a player after the
    // records before them, a round of the same player at once and a switch of the player after the queue, the end of the
    // match stops the queue quietly; a failed record (a throwing or missing handler, in the queue or at once) ends the match
    // with an error and nothing is applied after it.
    // The card handler stands for an animation: it does not complete until the test lets it.
    public class SnapshotReceiverTests
    {
        private const int _waitFrames = 300;

        private readonly Guid _a = Guid.NewGuid();
        private readonly Guid _b = Guid.NewGuid();

        private Lifetime _lifetime;
        private FakeLog<SnapshotReceiver> _log;
        private FakeGameState _gameState;
        private SnapshotReceiver _receiver;
        private List<string> _handled;
        private CardAnimation _cardAnimation;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();
            _log = new FakeLog<SnapshotReceiver>();
            _gameState = new FakeGameState();
            _receiver = new SnapshotReceiver(_gameState, _log);

            var handled = new List<string>();
            var cardAnimation = new CardAnimation();
            _handled = handled;
            _cardAnimation = cardAnimation;

            Handle<SharedBoardSnapshot>(handled);
            Handle<PlayerSnapshotRecord.ManaUpdate>(handled);
            Handle<TimeLimitedRoundRecord>(handled);
            Handle<LastManStandingRoundRecord>(handled);
            _receiver.Add(typeof(PlayerSnapshotRecord.CardUse), record =>
            {
                handled.Add(Describe(record));
                return cardAnimation.Play();
            });

            _receiver.OnSetup(_lifetime);
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
        }

        [Test]
        public void Received_EverySnapshotAtOnce()
        {
            var received = new List<SharedMoveSnapshot>();
            ((IReceivedSnapshots)_receiver).Received.Advise(_lifetime, snapshot => received.Add(snapshot));
            var first = Snapshot(Mana(1));
            var second = Snapshot(Card());

            Push(first);
            Push(second);

            CollectionAssert.AreEqual(new[] { first, second }, received);
        }

        [UnityTest]
        public IEnumerator BoardFlags_AtOnce_TheRestOfTheBoardQueuedInOrder() => UniTask.ToCoroutine(async () =>
        {
            Push(Snapshot(Board(_a, Free(1, 1), Flag(2, 2, true), Mines(1, 1, 2), Flag(3, 3, false))));

            CollectionAssert.AreEqual(new[] { "board A: flag 2,2 True", "board A: flag 3,3 False" }, _handled);

            await WaitHandled(3);

            Assert.AreEqual("board A: free 1,1, mines 1,1 2", _handled[2]);
        });

        [UnityTest]
        public IEnumerator BoardFlags_WhileAnAnimationPlays_BeforeTheQueuedBoardRecords() => UniTask.ToCoroutine(async () =>
        {
            var animation = HoldCardAnimation();
            Push(Snapshot(Card()));
            await WaitHandled(1);

            Push(Snapshot(Board(_b, Free(0, 0), Flag(1, 1, true))));

            CollectionAssert.AreEqual(new[] { "card", "board B: flag 1,1 True" }, _handled);

            await Frames(5);
            Assert.AreEqual(2, _handled.Count);

            animation.TrySetResult();
            await WaitHandled(3);

            Assert.AreEqual("board B: free 0,0", _handled[2]);
        });

        [UnityTest]
        public IEnumerator Queue_EveryRecordWaitsForTheOnesBefore_InTheOrderReceived() => UniTask.ToCoroutine(async () =>
        {
            var animation = HoldCardAnimation();

            Push(Snapshot(Card(), Board(_a, Free(4, 4))));
            Push(Snapshot(Mana(3)));
            await WaitHandled(1);
            await Frames(5);

            CollectionAssert.AreEqual(new[] { "card" }, _handled);

            animation.TrySetResult();
            await WaitHandled(3);

            CollectionAssert.AreEqual(new[] { "card", "board A: free 4,4", "mana 3" }, _handled);
        });

        [UnityTest]
        public IEnumerator Mana_AfterTheCardAnimation() => UniTask.ToCoroutine(async () =>
        {
            var animation = HoldCardAnimation();

            Push(Snapshot(Card(), Mana(2)));
            await WaitHandled(1);
            await Frames(5);

            CollectionAssert.AreEqual(new[] { "card" }, _handled);

            animation.TrySetResult();
            await WaitHandled(2);

            Assert.AreEqual("mana 2", _handled[1]);
        });

        [UnityTest]
        public IEnumerator TimeLimitedRound_SamePlayer_AtOnce() => UniTask.ToCoroutine(() => SamePlayer_AtOnce(TimeLimited));

        [UnityTest]
        public IEnumerator LastManStandingRound_SamePlayer_AtOnce() => UniTask.ToCoroutine(() => SamePlayer_AtOnce(LastManStanding));

        [UnityTest]
        public IEnumerator TimeLimitedRound_AnotherPlayer_AfterTheQueue() => UniTask.ToCoroutine(() => AnotherPlayer_AfterTheQueue(TimeLimited));

        [UnityTest]
        public IEnumerator LastManStandingRound_AnotherPlayer_AfterTheQueue() =>
            UniTask.ToCoroutine(() => AnotherPlayer_AfterTheQueue(LastManStanding));

        [UnityTest]
        public IEnumerator LifetimeTerminatedWhileARecordIsShown_LoopEndsWithoutAnError() => UniTask.ToCoroutine(async () =>
        {
            _cardAnimation.Set(() => UniTask.Delay(TimeSpan.FromMinutes(1), cancellationToken: _lifetime.Token));

            Push(Snapshot(Card()));
            await WaitHandled(1);

            _lifetime.Terminate();
            await Frames(5);
            Push(Snapshot(Mana(1)));
            await Frames(5);

            CollectionAssert.AreEqual(new[] { "card" }, _handled);
            Assert.IsEmpty(_log.Entries);
        });

        [UnityTest]
        public IEnumerator RecordsQueuedBeforeTheEnd_NotHandledAfterIt() => UniTask.ToCoroutine(async () =>
        {
            Push(Snapshot(Mana(1), Board(_a, Free(0, 0))));
            _lifetime.Terminate();
            await Frames(5);

            Push(Snapshot(Mana(2)));
            await Frames(5);

            Assert.IsEmpty(_handled);
            Assert.IsEmpty(_log.Entries);
            Assert.AreEqual(0, _gameState.Failures);
        });

        [UnityTest]
        public IEnumerator TheRestOfTheQueue_AfterTheEndWhileARecordIsShown_NotHandled() => UniTask.ToCoroutine(async () =>
        {
            var animation = HoldCardAnimation();

            Push(Snapshot(Card(), Mana(1)));
            await WaitHandled(1);

            _lifetime.Terminate();
            animation.TrySetResult();
            await Frames(5);

            CollectionAssert.AreEqual(new[] { "card" }, _handled);
            Assert.IsEmpty(_log.Entries);
        });

        [UnityTest]
        public IEnumerator QueuedHandlerFails_MatchFailsWithTheError_NothingIsHandledAfterIt() => UniTask.ToCoroutine(async () =>
        {
            _cardAnimation.Set(() => UniTask.FromException(new InvalidOperationException("broken card")));

            Push(Snapshot(Card(), Mana(5)));
            await WaitFailures(1);
            Push(Snapshot(Board(_a, Flag(1, 1, true)), Mana(6)));
            await Frames(5);

            CollectionAssert.AreEqual(new[] { "card" }, _handled);
            AssertFailedWith<InvalidOperationException>();
        });

        [UnityTest]
        public IEnumerator QueuedRecordWithoutHandler_MatchFails() => UniTask.ToCoroutine(async () =>
        {
            Push(Snapshot(new PlayerSnapshotRecord.HealthUpdate { PlayerId = _a, Current = 1 }, Mana(4)));
            await WaitFailures(1);
            await Frames(5);

            Assert.IsEmpty(_handled);
            AssertFailedWith<ArgumentException>();
        });

        [Test]
        public void ImmediateRecordWithoutHandler_MatchFails()
        {
            var log = new FakeLog<SnapshotReceiver>();
            var gameState = new FakeGameState();
            var receiver = new SnapshotReceiver(gameState, log);

            ((INetworkCommand)receiver).Execute(_lifetime, Snapshot(Board(_a, Flag(0, 0, true))));

            Assert.AreEqual(1, gameState.Failures);
            Assert.AreEqual(1, log.Entries.Count);
            Assert.AreEqual(LogLevel.Error, log.Entries[0].Level);
            Assert.IsInstanceOf<ArgumentException>(log.Entries[0].Exception);
        }

        [Test]
        public void ImmediateHandlerFails_MatchFails_TheSnapshotsAfterItAreNotApplied()
        {
            var received = new List<SharedMoveSnapshot>();
            ((IReceivedSnapshots)_receiver).Received.Advise(_lifetime, snapshot => received.Add(snapshot));
            _receiver.Add(typeof(SharedBoardSnapshot), _ => UniTask.FromException(new InvalidOperationException("broken flag")));
            var failed = Snapshot(Board(_a, Flag(0, 0, true)));

            Push(failed);
            Push(Snapshot(Board(_a, Flag(1, 1, true))));

            CollectionAssert.AreEqual(new[] { failed }, received);
            AssertFailedWith<InvalidOperationException>();
        }

        [Test]
        public void ImmediateHandlerPending_MatchFails()
        {
            var pending = new UniTaskCompletionSource();
            _receiver.Add(typeof(SharedBoardSnapshot), _ => pending.Task);

            Push(Snapshot(Board(_a, Flag(0, 0, true))));

            AssertFailedWith<InvalidOperationException>();
            pending.TrySetResult();
        }

        [UnityTest]
        public IEnumerator SameRoundPlayer_HandlerFails_MatchFails() => UniTask.ToCoroutine(async () =>
        {
            Push(Snapshot(LastManStanding(_a)));
            await WaitHandled(1);
            _receiver.Add(typeof(LastManStandingRoundRecord), _ => UniTask.FromException(new InvalidOperationException("broken round")));

            Push(Snapshot(LastManStanding(_a)));

            AssertFailedWith<InvalidOperationException>();
        });

        private async UniTask SamePlayer_AtOnce(Func<Guid, IMoveSnapshotRecord> round)
        {
            // The first round is queued: no player is remembered before it is handled.
            Push(Snapshot(round(_a)));
            Assert.IsEmpty(_handled);
            await WaitHandled(1);

            var animation = HoldCardAnimation();
            Push(Snapshot(Card()));
            await WaitHandled(2);

            Push(Snapshot(round(_a)));

            CollectionAssert.AreEqual(new[] { "round A", "card", "round A" }, _handled);
            animation.TrySetResult();
        }

        private async UniTask AnotherPlayer_AfterTheQueue(Func<Guid, IMoveSnapshotRecord> round)
        {
            Push(Snapshot(round(_a)));
            await WaitHandled(1);

            var animation = HoldCardAnimation();
            Push(Snapshot(Card()));
            await WaitHandled(2);

            Push(Snapshot(round(_b)));
            await Frames(5);

            CollectionAssert.AreEqual(new[] { "round A", "card" }, _handled);

            animation.TrySetResult();
            await WaitHandled(3);

            Assert.AreEqual("round B", _handled[2]);

            // The handled switch is remembered: the next round of the same player is at once.
            var next = HoldCardAnimation();
            Push(Snapshot(Card()));
            await WaitHandled(4);
            Push(Snapshot(round(_b)));

            CollectionAssert.AreEqual(new[] { "round A", "card", "round B", "card", "round B" }, _handled);
            next.TrySetResult();
        }

        private UniTaskCompletionSource HoldCardAnimation()
        {
            var animation = new UniTaskCompletionSource();
            _cardAnimation.Set(() => animation.Task);
            return animation;
        }

        private void Handle<T>(List<string> handled) where T : IMoveSnapshotRecord
        {
            _receiver.Add(typeof(T), record =>
            {
                handled.Add(Describe(record));
                return UniTask.CompletedTask;
            });
        }

        private void Push(SharedMoveSnapshot snapshot)
        {
            ((INetworkCommand)_receiver).Execute(_lifetime, snapshot);
        }

        private void AssertFailedWith<TException>() where TException : Exception
        {
            Assert.AreEqual(1, _gameState.Failures);
            Assert.AreEqual(1, _log.Entries.Count);
            Assert.AreEqual(LogLevel.Error, _log.Entries[0].Level);
            Assert.IsInstanceOf<TException>(_log.Entries[0].Exception);
        }

        private async UniTask WaitFailures(int count)
        {
            for (var frame = 0; _gameState.Failures < count; frame++)
            {
                if (frame >= _waitFrames)
                    Assert.Fail($"Waited {_waitFrames} frames for {count} failures of the match, got {_gameState.Failures}");

                await UniTask.Yield();
            }
        }

        private async UniTask WaitHandled(int count)
        {
            for (var frame = 0; _handled.Count < count; frame++)
            {
                if (frame >= _waitFrames)
                    Assert.Fail($"Waited {_waitFrames} frames for {count} handled records, handled: {string.Join(" | ", _handled)}");

                await UniTask.Yield();
            }
        }

        private static async UniTask Frames(int count)
        {
            for (var i = 0; i < count; i++)
                await UniTask.Yield();
        }

        private string Describe(IMoveSnapshotRecord record)
        {
            return record switch
            {
                SharedBoardSnapshot board => $"board {Name(board.BoardOwnerId)}: {string.Join(", ", board.Records.Select(Describe))}",
                PlayerSnapshotRecord.ManaUpdate mana => $"mana {mana.Current}",
                PlayerSnapshotRecord.CardUse => "card",
                TimeLimitedRoundRecord timeLimited => $"round {Name(timeLimited.CurrentPlayer)}",
                LastManStandingRoundRecord lastManStanding => $"round {Name(lastManStanding.CurrentPlayer)}",
                _ => throw new ArgumentOutOfRangeException(nameof(record), record, null)
            };
        }

        private static string Describe(IBoardSnapshotRecord record)
        {
            return record switch
            {
                BoardSnapshotRecord.Flag flag => $"flag {flag.Position.x},{flag.Position.y} {flag.IsFlagged}",
                BoardSnapshotRecord.CellFree free => $"free {free.Position.x},{free.Position.y}",
                BoardSnapshotRecord.MinesAround mines => $"mines {mines.Position.x},{mines.Position.y} {mines.Count}",
                _ => throw new ArgumentOutOfRangeException(nameof(record), record, null)
            };
        }

        private string Name(Guid player)
        {
            if (player == _a)
                return "A";

            if (player == _b)
                return "B";

            throw new ArgumentOutOfRangeException(nameof(player), player, null);
        }

        private static SharedMoveSnapshot Snapshot(params IMoveSnapshotRecord[] records)
        {
            return new SharedMoveSnapshot { Records = records };
        }

        private static SharedBoardSnapshot Board(Guid owner, params IBoardSnapshotRecord[] records)
        {
            return new SharedBoardSnapshot { BoardOwnerId = owner, Records = new List<IBoardSnapshotRecord>(records) };
        }

        private static BoardSnapshotRecord.Flag Flag(int x, int y, bool isFlagged)
        {
            return new BoardSnapshotRecord.Flag { Position = new Position(x, y), IsFlagged = isFlagged };
        }

        private static BoardSnapshotRecord.CellFree Free(int x, int y)
        {
            return new BoardSnapshotRecord.CellFree { Position = new Position(x, y) };
        }

        private static BoardSnapshotRecord.MinesAround Mines(int x, int y, int count)
        {
            return new BoardSnapshotRecord.MinesAround { Position = new Position(x, y), Count = count };
        }

        private static PlayerSnapshotRecord.ManaUpdate Mana(int current)
        {
            return new PlayerSnapshotRecord.ManaUpdate { Current = current };
        }

        private static PlayerSnapshotRecord.CardUse Card()
        {
            return new PlayerSnapshotRecord.CardUse();
        }

        private static IMoveSnapshotRecord TimeLimited(Guid player)
        {
            return new TimeLimitedRoundRecord { CurrentPlayer = player, SecondsLeft = new Dictionary<Guid, long>() };
        }

        private static IMoveSnapshotRecord LastManStanding(Guid player)
        {
            return new LastManStandingRoundRecord { CurrentPlayer = player };
        }

        // What the card handler waits for: by default nothing, a test sets a held or a failing animation.
        private sealed class CardAnimation
        {
            private Func<UniTask> _play = () => UniTask.CompletedTask;

            public void Set(Func<UniTask> play)
            {
                _play = play;
            }

            public UniTask Play()
            {
                return _play();
            }
        }
    }
}
