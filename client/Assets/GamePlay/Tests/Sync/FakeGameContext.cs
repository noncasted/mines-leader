using System;
using System.Collections.Generic;
using GamePlay.Loop;
using GamePlay.Players;

namespace GamePlay.Tests
{
    // The two players of a match: the local one and the other one.
    public class FakeGameContext : IGameContext
    {
        public FakeGameContext(IGamePlayer self, IGamePlayer other)
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
}
