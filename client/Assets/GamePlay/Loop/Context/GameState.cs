using System;
using Common.Network;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Loop
{
    public enum GameStateType
    {
        WaitingFoPlayers,
        Active,
        Completed
    }

    public interface IGameState
    {
        IViewableProperty<GameStateType> Value { get; }
        IViewableProperty<MatchCompletedData> CompletedData { get; }

        void Set(GameStateType type);
        UniTask<MatchCompletedData> WaitCompletion(IReadOnlyLifetime lifetime);
        void OnLeave();
    }

    public class GameState : NetworkService, IGameState
    {
        public GameState(
            IGameContext context,
            NetworkProperty<GameFlowState> state)
        {
            _context = context;
            _state = state;
        }

        private readonly IGameContext _context;
        private readonly NetworkProperty<GameFlowState> _state;
        private readonly UniTaskCompletionSource<MatchCompletedData> _completion = new();

        private readonly ViewableProperty<GameStateType> _value = new(GameStateType.WaitingFoPlayers);
        private readonly ViewableProperty<MatchCompletedData> _completed = new();

        public IViewableProperty<GameStateType> Value => _value;
        public IViewableProperty<MatchCompletedData> CompletedData => _completed;

        public override void OnStarted(IReadOnlyLifetime lifetime)
        {
            _state.Advise(lifetime, state =>
                {
                    if (state.Winner == Guid.Empty)
                        return;

                    var player = _context.GetPlayer(state.Winner);

                    _completion.TrySetResult(new MatchCompletedData()
                        {
                            Type = player.Info.IsLocal == true ? MatchResultType.Win : MatchResultType.Lose
                        });
                });
        }

        public void Set(GameStateType type)
        {
            _value.Set(type);
        }

        public async UniTask<MatchCompletedData> WaitCompletion(IReadOnlyLifetime lifetime)
        {
            var data = await _completion.Task;
            _completed.Set(data);
            return data;
        }

        public void OnLeave()
        {
            _completion.TrySetResult(new MatchCompletedData()
                {
                    Type = MatchResultType.Leave
                });
        }
    }
}