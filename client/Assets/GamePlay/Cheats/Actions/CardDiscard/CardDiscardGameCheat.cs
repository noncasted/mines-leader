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
    public class CardDiscardGameCheat : MonoBehaviour, ISceneService, IMatchStarted
    {
        [SerializeField] private CardDiscardEntry _prefab;
        [SerializeField] private RectTransform _root;

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
            var hand = localPlayer.Hand;

            hand.Entries.View(lifetime, (cardLifetime, card) =>
                {
                    var view = Instantiate(_prefab, _root);
                    view.Setup(card.Definition);
                    cardLifetime.Listen(() => Destroy(view.gameObject));

                    view.Clicked.Advise(lifetime, () => _connection.Request(new GameCheatContexts.CardRemove()
                            {
                                Type = card.Definition.Type,
                                EntityId = card.EntityId
                            }
                        )
                    );
                }
            );
        }
    }
}