using Common.Network;
using Internal;
using Meta;
using Shared;
using UnityEngine;
using VContainer;

namespace GamePlay.Cheats
{
    [DisallowMultipleComponent]
    public class CardAddGameCheat : MonoBehaviour, ISceneService, IScopeSetup
    {
        [SerializeField] private CardAddEntry _prefab;
        [SerializeField] private RectTransform _root;

        private INetworkConnection _connection;
        private ICardsRegistry _cards;

        [Inject]
        private void Construct(INetworkConnection connection, ICardsRegistry cards)
        {
            _cards = cards;
            _connection = connection;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IScopeSetup>();

        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            foreach (var (type, definition) in _cards.Entries)
            {
                var entry = Instantiate(_prefab, _root);
                entry.Setup(definition);

                entry.Clicked.Advise(lifetime, () => _connection.Request(new GameCheatContexts.CardAdd()
                        {
                            Type = definition.Type
                        }
                    )
                );
            }
        }
    }
}