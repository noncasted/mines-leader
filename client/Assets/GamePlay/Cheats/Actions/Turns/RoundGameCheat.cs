using Common.Network;
using GamePlay.Loop;
using Global.UI;
using Internal;
using Shared;
using UnityEngine;
using VContainer;

namespace GamePlay.Cheats
{
    [DisallowMultipleComponent]
    public class RoundGameCheat : MonoBehaviour, IScopeSetup, ISceneService
    {
        [SerializeField] private DesignButton _restoreMovesButton;
        [SerializeField] private DesignButton _winButton;
        [SerializeField] private DesignButton _loseButton;
        
        private INetworkConnection _connection;
        private IGameRound _round;
        private IGameContext _gameContext;

        [Inject]
        private void Construct(INetworkConnection connection, IGameRound round, IGameContext gameContext)
        {
            _gameContext = gameContext;
            _connection = connection;
            _round = round;
        }
        
        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IScopeSetup>();
        }
        
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _restoreMovesButton.ListenClick(lifetime, () =>
            {
                if (_round.IsTurnAllowed == false)
                    return;
                
                _connection.Request(new GameCheatContexts.ChangeMoves()
                {
                    Value = 100000
                });
            });
            
            _winButton.ListenClick(lifetime, () =>
            {
                _connection.Request(new GameCheatContexts.EndMatch()
                {
                    Winner = _gameContext.Self.Id
                });
            });
            
            _loseButton.ListenClick(lifetime, () =>
            {
                _connection.Request(new GameCheatContexts.EndMatch()
                {
                    Winner = _gameContext.Other.Id
                });
            });
        }
    }
}