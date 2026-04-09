using Common.Network;
using GamePlay.Loop;
using GamePlay.Players;
using Internal;
using Shared;
using UnityEngine;
using VContainer;

namespace GamePlay.Cheats
{
    [DisallowMultipleComponent]
    public class MaxManaAutoCheat : MonoBehaviour, ISceneService, IMatchStarted
    {
        [SerializeField] private int _maxMana = 10;

        private INetworkConnection _connection;

        [Inject]
        private void Construct(INetworkConnection connection)
        {
            _connection = connection;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IMatchStarted>();
        }

        public void OnMatchStarted(IReadOnlyLifetime lifetime, IGamePlayer localPlayer)
        {
            _connection.Request(new GameCheatContexts.ChangeMaxMana()
                {
                    Value = _maxMana
                });
        }
    }
}
