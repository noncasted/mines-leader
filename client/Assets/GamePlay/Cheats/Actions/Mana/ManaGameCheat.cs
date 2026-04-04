using Common.Network;
using GamePlay.Loop;
using GamePlay.Players;
using Global.UI;
using Internal;
using Shared;
using UnityEngine;
using VContainer;

namespace GamePlay.Cheats
{
    [DisallowMultipleComponent]
    public class ManaGameCheat : MonoBehaviour, ISceneService, IMatchStarted
    {
        [SerializeField] private DesignButton _minButton;
        [SerializeField] private DesignButton _maxButton;
        [SerializeField] private DesignButton _addButton;
        [SerializeField] private DesignButton _removeButton;

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
            _minButton.ListenClick(lifetime, () => Send(-10000));
            _maxButton.ListenClick(lifetime, () => Send(10000));
            _addButton.ListenClick(lifetime, () => Send(1));
            _removeButton.ListenClick(lifetime, () => Send(-1));

            return;

            void Send(int change)
            {
                _connection.Request(new GameCheatContexts.ChangeMana()
                    {
                        Value = change
                    }
                );
            }
        }
    }
}