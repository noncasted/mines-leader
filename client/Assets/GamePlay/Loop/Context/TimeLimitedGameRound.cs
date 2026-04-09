using System;
using Common.Network;
using GamePlay.Players;
using Internal;
using Shared;

namespace GamePlay.Loop
{
    public class TimeLimitedGameRound : NetworkService, IGameRound
    {
        public TimeLimitedGameRound(
            INetworkConnection connection,
            IGameContext gameContext,
            NetworkProperty<TimeLimitedRoundState> state)
        {
            _connection = connection;
            _gameContext = gameContext;
            _roundTime = new ViewableProperty<float>(30);
            _state = state;
        }

        private readonly INetworkConnection _connection;
        private readonly IGameContext _gameContext;

        private readonly NetworkProperty<TimeLimitedRoundState> _state;

        private readonly ViewableProperty<float> _roundTime;
        private readonly ViewableProperty<IGamePlayer> _player = new();

        public bool IsTurnAllowed => _gameContext.Self.Id == _state.Value.CurrentPlayer;

        public IViewableProperty<IGamePlayer> Player => _player;
        public IViewableProperty<float> RoundTime => _roundTime;

        public override void OnStarted(IReadOnlyLifetime lifetime)
        {
            _state.Advise(lifetime, state =>
                {
                    if (state.CurrentPlayer == Guid.Empty)
                        return;

                    var player = _gameContext.GetPlayer(state.CurrentPlayer);
                    _player.Set(player);
                    _roundTime.Set(state.SecondsLeft[player.Id]);
                });
        }

        public void TrySkip()
        {
            if (IsTurnAllowed == false)
                return;

            _connection.Request(new SharedGameAction.SkipTurn());
        }
    }
}