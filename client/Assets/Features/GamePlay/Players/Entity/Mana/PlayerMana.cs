using Common.Network.Common;
using GamePlay.Loop;
using Internal;

namespace GamePlay.Players
{
    public class PlayerMana : IPlayerMana, IScopeLoaded
    {
        public PlayerMana(
            IGameContext gameContext,
            IGameRound gameRound,
            NetworkProperty<PlayerManaState> state)
        {
            _gameContext = gameContext;
            _gameRound = gameRound;
            _state = state;
        }

        private readonly IGameContext _gameContext;
        private readonly IGameRound _gameRound;
        
        private readonly NetworkProperty<PlayerManaState> _state;

        private readonly ViewableProperty<int> _current = new();
        private readonly ViewableProperty<int> _max = new();

        private float _regenerationTimer;
        private GameOptions Options => _gameContext.Options;
        private PlayerManaState State => _state.Value;

        public IViewableProperty<int> Current => _current;
        public IViewableProperty<int> Max => _max;

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _gameRound.Player.Advise(lifetime, player =>
            {
                if (player.Info.IsLocal == false)
                    return;

                State.Current += Options.ManaGain;

                if (State.Current > Options.MaxMana)
                    State.Current = Options.MaxMana;
                
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
    }
}