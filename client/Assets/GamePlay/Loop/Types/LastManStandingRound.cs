using System;
using GamePlay.Players;
using Internal;
using Shared;

namespace GamePlay.Loop
{
    public interface ILastManStandingRound : IGameRound
    {
        void Apply(Guid currentPlayer, int currentRound, int secondsLeft);
    }

    public class LastManStandingRound : ILastManStandingRound
    {
        public LastManStandingRound(
            INetworkConnection connection,
            IGameContext gameContext)
        {
            _connection = connection;
            _gameContext = gameContext;
            _roundTime = new ViewableProperty<float>(30);
        }

        private readonly INetworkConnection _connection;
        private readonly IGameContext _gameContext;

        private readonly ViewableProperty<float> _roundTime;
        private readonly ViewableProperty<IGamePlayer> _player = new();

        private Guid _currentPlayerId;

        public bool IsTurnAllowed => _gameContext.Self.Id == _currentPlayerId;

        public IViewableProperty<IGamePlayer> Player => _player;
        public IViewableProperty<float> RoundTime => _roundTime;

        public void Apply(Guid currentPlayer, int currentRound, int secondsLeft)
        {
            _currentPlayerId = currentPlayer;

            if (currentPlayer == Guid.Empty)
                return;

            var player = _gameContext.GetPlayer(currentPlayer);
            _player.Set(player);
            _roundTime.Set(secondsLeft);
        }

        public void TrySkip()
        {
            if (IsTurnAllowed == false)
                return;

            _connection.Request(new SharedGameAction.SkipTurn());
        }
    }
}
