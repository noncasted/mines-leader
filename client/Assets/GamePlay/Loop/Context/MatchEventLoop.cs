using System;
using System.Collections.Generic;
using GamePlay.Players;
using Internal;
using VContainer.Internal;

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

    public class MatchEventLoop : IScopeSetup
    {
        public MatchEventLoop(
            IGameState state,
            IGameContext context,
            ContainerLocal<IReadOnlyList<IWaitingForPlayers>> waitingForPlayers,
            ContainerLocal<IReadOnlyList<IMatchStarted>> matchStarted,
            ContainerLocal<IReadOnlyList<IMatchCompleted>> matchCompleted)
        {
            _state = state;
            _context = context;
            _waitingForPlayers = waitingForPlayers.Value;
            _matchStarted = matchStarted.Value;
            _matchCompleted = matchCompleted.Value;
        }

        private readonly IGameState _state;
        private readonly IGameContext _context;

        private readonly IReadOnlyList<IWaitingForPlayers> _waitingForPlayers;
        private readonly IReadOnlyList<IMatchStarted> _matchStarted;
        private readonly IReadOnlyList<IMatchCompleted> _matchCompleted;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _state.Value.View(lifetime, (stateLifetime, state) =>
                {
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
                }
            );
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
    }
}