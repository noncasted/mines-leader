using Global.Backend;
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

        private CardsRegistry _cards;
        private INetworkConnection _connection;

        [Inject]
        private void Construct(INetworkConnection connection)
        {
            _connection = connection;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IScopeSetup>();

            _cards = builder.GetAsset<CardsRegistry>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            foreach (var definition in _cards.Objects)
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