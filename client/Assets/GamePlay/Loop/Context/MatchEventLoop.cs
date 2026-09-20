using System;
using System.Collections.Generic;
using GamePlay.Players;
using Internal;

namespace GamePlay.Loop
{
    public interface IWaitingForPlayers
    {
        void OnWaitingForPlayers(IReadOnlyLifetime lifetime);
    }

    public interface IMatchStarted
    {
        void OnMatchStarted(IReadOnlyLifetime lifetime, IGamePlayer localPlayer);
    }

    public interface IMatchCompleted
    {
        void OnMatchCompleted(IReadOnlyLifetime lifetime, MatchCompletedData data);
    }

    public interface IRoundChanged
    {
        void OnRoundChanged(IReadOnlyLifetime lifetime, IGamePlayer player);
    }

    public class MatchEventLoop : IScopeSetup
    {
        public MatchEventLoop(
            IGameState state,
            IGameContext context,
            IGameRound round,
            IReadOnlyList<IWaitingForPlayers> waitingForPlayers,
            IReadOnlyList<IMatchStarted> matchStarted,
            IReadOnlyList<IMatchCompleted> matchCompleted,
            IReadOnlyList<IRoundChanged> roundChanged)
        {
            _state = state;
            _context = context;
            _round = round;
            _waitingForPlayers = waitingForPlayers;
            _matchStarted = matchStarted;
            _matchCompleted = matchCompleted;
            _roundChanged = roundChanged;
        }

        private readonly IGameState _state;
        private readonly IGameContext _context;
        private readonly IGameRound _round;

        private readonly IReadOnlyList<IWaitingForPlayers> _waitingForPlayers;
        private readonly IReadOnlyList<IMatchStarted> _matchStarted;
        private readonly IReadOnlyList<IMatchCompleted> _matchCompleted;
        private readonly IReadOnlyList<IRoundChanged> _roundChanged;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _state.Value.View(lifetime, (stateLifetime, state) => {
                switch (state)
                {
                    case GameStateType.WaitingFoPlayers:
                        OnWaitingForPlayers(stateLifetime);
                        break;
                    case GameStateType.Active:
                        OnMatchStarted(stateLifetime, _context.Self);
                        break;
                    case GameStateType.Completed:
                        OnMatchCompleted(stateLifetime, _state.CompletedData.Value);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(state), state, null);
                }
            });

            // Раунд живёт дольше состояния матча, поэтому слушается отдельно от него.
            _round.Player.View(lifetime, (playerLifetime, player) => OnRoundChanged(playerLifetime, player));
        }

        private void OnWaitingForPlayers(IReadOnlyLifetime lifetime)
        {
            foreach (var listener in _waitingForPlayers)
                listener.OnWaitingForPlayers(lifetime);
        }

        private void OnMatchStarted(IReadOnlyLifetime lifetime, IGamePlayer localPlayer)
        {
            foreach (var listener in _matchStarted)
                listener.OnMatchStarted(lifetime, localPlayer);
        }

        private void OnMatchCompleted(IReadOnlyLifetime lifetime, MatchCompletedData data)
        {
            foreach (var listener in _matchCompleted)
                listener.OnMatchCompleted(lifetime, data);
        }

        private void OnRoundChanged(IReadOnlyLifetime lifetime, IGamePlayer player)
        {
            foreach (var listener in _roundChanged)
                listener.OnRoundChanged(lifetime, player);
        }
    }
}