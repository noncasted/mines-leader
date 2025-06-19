using Common.Network.Common;
using GamePlay.Loop;
using Internal;

namespace GamePlay.Players
{
    public class PlayerTurns : IPlayerTurns, IScopeLoaded
    {
        public PlayerTurns(
            IGameRound gameRound,
            IGamePlayerInfo info,
            NetworkProperty<PlayerTurnsState> state)
        {
            _gameRound = gameRound;
            _info = info;
            _state = state;
            state.Set(new PlayerTurnsState());
        }

        private readonly IGameRound _gameRound;
        private readonly IGamePlayerInfo _info;
        private readonly NetworkProperty<PlayerTurnsState> _state;

        private readonly ViewableProperty<bool> _isTurn = new(false);
        private readonly ViewableProperty<int> _current = new();
        private readonly ViewableProperty<int> _max = new();
        private readonly ViewableDelegate _start = new();
        private readonly ViewableDelegate _end = new();

        private PlayerTurnsState State => _state.Value;

        public IViewableProperty<bool> IsTurn => _isTurn;

        public IViewableProperty<int> Current => _current;
        public IViewableProperty<int> Max => _max;

        public IViewableDelegate Start => _start;
        public IViewableDelegate End => _end;

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _gameRound.Player.Advise(lifetime, player =>
            {
                if (player.Info.Id != _info.Id)
                {
                    _isTurn.Set(false);
                    return;
                }

                _isTurn.Set(true);
                State.Current = _max.Value;
                _state.MarkDirty();
            });

            _state.View(lifetime, state =>
            {
                _current.Set(state.Current);
                _max.Set(state.Max);
            });
        }

        public void SetCurrent(int amount)
        {
            State.Current = amount;
            _state.MarkDirty();
        }

        public void SetMax(int amount)
        {
            State.Max = amount;
            _state.MarkDirty();
        }

        public void OnUsed()
        {
            State.Current--;

            if (State.Current == 0)
                _gameRound.OnLocalTurnCompleted();

            _state.MarkDirty();
        }
    }
}