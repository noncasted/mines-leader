using System;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Shared;

namespace GamePlay.Tests
{
    // The state of a match for the tests of SnapshotReceiver: it counts the failures of the match, any other call fails.
    public class FakeGameState : IGameState
    {
        public int Failures { get; private set; }

        public IViewableProperty<GameStateType> Value => throw new NotSupportedException();
        public IViewableProperty<MatchCompletedData> CompletedData => throw new NotSupportedException();

        public void Set(GameStateType type) => throw new NotSupportedException();
        public void SetWinner(GameCompletedRecord record) => throw new NotSupportedException();
        public UniTask<MatchCompletedData> WaitCompletion(IReadOnlyLifetime lifetime) => throw new NotSupportedException();
        public void OnLeave() => throw new NotSupportedException();

        public void OnFailed()
        {
            Failures++;
        }
    }
}
