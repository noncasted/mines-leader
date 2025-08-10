using Common.Network;
using GamePlay.Players;
using Internal;
using Shared;

namespace GamePlay.Loop
{
    public interface IGameRound
    {
        bool IsTurnAllowed { get; }
        IViewableProperty<IGamePlayer> Player { get; }
        IViewableProperty<float> RoundTime { get; }

        void TrySkip();
    }

    public class GameRound : NetworkService, IGameRound
    {
        public GameRound(IGameContext gameContext, NetworkProperty<GameRoundState> state)
        {
            _gameContext = gameContext;
            _roundTime = new ViewableProperty<float>(30);
            _state = state;
        }

        private readonly IGameContext _gameContext;

        private readonly NetworkProperty<GameRoundState> _state;

        private readonly ViewableProperty<float> _roundTime;
        private readonly ViewableProperty<IGamePlayer> _player = new();

        public bool IsTurnAllowed => _gameContext.Self.Id == _state.Value.CurrentPlayer;

        public IViewableProperty<IGamePlayer> Player => _player;
        public IViewableProperty<float> RoundTime => _roundTime;

        public override void OnStarted(IReadOnlyLifetime lifetime)
        {
            _state.Advise(lifetime, state =>
            {
                var player = _gameContext.GetPlayer(state.CurrentPlayer);
                _player.Set(player);
                _roundTime.Set(state.SecondsLeft);
            });
        }

        public void TrySkip()
        {
            if (IsTurnAllowed == false)
                return;
            
        }
    }
}