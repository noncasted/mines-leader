using System;
using Common.Network;
using GamePlay.Players;
using Global.Systems;
using Internal;
using MemoryPack;

namespace GamePlay.Loop
{
    public interface IGameRound
    {
        bool IsTurnAllowed { get; }
        IViewableProperty<IGamePlayer> Player { get; }
        IViewableProperty<float> RoundTime { get; }
        
        void Start();
        void OnLocalTurnCompleted();
        void TrySkip();
    }
    
    public class GameRound : NetworkService, IGameRound, IUpdatable
    {
        public GameRound(
            IUpdater updater,
            IGameContext gameContext,
            NetworkProperty<GameTurnsState> state)
        {
            _updater = updater;
            _gameContext = gameContext;
            _timer = gameContext.Options.RoundTime;
            _roundTime = new ViewableProperty<float>(_timer);
            _state = state;
        }

        private readonly IUpdater _updater;
        private readonly IGameContext _gameContext;

        private readonly NetworkProperty<GameTurnsState> _state;

        private readonly ViewableProperty<float> _roundTime;
        private readonly ViewableProperty<IGamePlayer> _player = new();

        private float _timer;
        private bool _isInitialized;

        public bool IsTurnAllowed => _gameContext.Self.Id == _state.Value.ActivePlayerId;

        public IViewableProperty<IGamePlayer> Player => _player;
        public IViewableProperty<float> RoundTime => _roundTime;

        public override void OnStarted(IReadOnlyLifetime lifetime)
        {
            _state.Advise(lifetime, state =>
            {
                if (_isInitialized == false)
                {
                    _isInitialized = true;
                    _updater.Add(lifetime, this);
                }

                _timer = _gameContext.Options.RoundTime;

                var player = _gameContext.GetPlayer(state.ActivePlayerId);
                _player.Set(player);
            });
        }

        public void Start()
        {
            _state.Value.ActivePlayerId = _gameContext.Self.Id;
            _state.MarkDirty();
        }

        public void OnLocalTurnCompleted()
        {
            var next = GetNext();
            _state.Value.ActivePlayerId = next;
            _state.MarkDirty();
        }

        public void TrySkip()
        {
            if (IsTurnAllowed == false)
                return;

            OnLocalTurnCompleted();
        }

        private Guid GetNext()
        {
            var index = _gameContext.All.IndexOf(_gameContext.Self);
            index = (index + 1) % _gameContext.All.Count;
            return _gameContext.All[index].Id;
        }

        public void OnUpdate(float delta)
        {
            _timer -= delta;

            if (_timer <= 0f)
            {
                _timer = 0f;

                if (IsTurnAllowed == true)
                    OnLocalTurnCompleted();
            }

            _roundTime.Set(_timer);
        }
    }

    [MemoryPackable]
    public partial class GameTurnsState
    {
        public Guid ActivePlayerId { get; set; }
    }
}