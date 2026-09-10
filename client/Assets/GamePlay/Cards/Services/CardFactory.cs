using System;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Players;
using Internal;
using Meta;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardFactory
    {
        public CardFactory(
            IEntityScopeLoader entityScopeLoader,
            ICardViewFactory cardViewFactory,
            IGameContext gameContext,
            ICardConfigs configs,
            ICardsRegistry registry,
            ICardTargets cardTargets,
            IContainer parentScope)
        {
            _entityScopeLoader = entityScopeLoader;
            _cardViewFactory = cardViewFactory;
            _gameContext = gameContext;
            _configs = configs;
            _registry = registry;
            _cardTargets = cardTargets;
            _parentScope = parentScope;
        }

        private readonly IEntityScopeLoader _entityScopeLoader;
        private readonly ICardViewFactory _cardViewFactory;
        private readonly IGameContext _gameContext;
        private readonly ICardConfigs _configs;
        private readonly ICardsRegistry _registry;
        private readonly ICardTargets _cardTargets;
        private readonly IContainer _parentScope;

        public async UniTask Create(IReadOnlyLifetime lifetime, bool isLocal, Guid cardId, CardType cardType)
        {
            var gamePlayer = isLocal ? _gameContext.Self : _gameContext.Other;
            var definition = _registry.Entries[cardType];

            var parentScope = isLocal ? _gameContext.Self.Scope : _parentScope;
            var spawnPoint = isLocal ? _cardTargets.LocalSpawn : _cardTargets.RemoteSpawn;

            if (isLocal == true)
            {
                var view = _cardViewFactory.Create(
                    GamePlayPrefabs.CardLocal.GetComponent<CardLocalScopeEntity>(),
                    spawnPoint);
                var loadResult = await _entityScopeLoader.Load(lifetime, parentScope, view, Build);
                await loadResult.Get<ICardLocalSpawn>().Execute();
            }
            else
            {
                var view = _cardViewFactory.Create(
                    GamePlayPrefabs.CardRemote.GetComponent<CardRemoteScopeEntity>(),
                    spawnPoint);
                var loadResult = await _entityScopeLoader.Load(lifetime, parentScope, view, Build);
                await loadResult.Get<ICardRemoteSpawn>().Execute();
            }

            [ContainerScopeParent(typeof(GamePlayScopeExtensions), nameof(GamePlayScopeExtensions.Construct))]
            void Build(IEntityBuilder builder)
            {
                builder.RegisterInstance(cardId);

                if (isLocal == true)
                {
                    builder
                        .AddCardLocalComponents()
                        .AddCardLocalRoot()
                        .AddCardLocalStates();

                    builder.RegisterInstance(_configs.Value.All[definition.Type]);

                    builder.RegisterInstance(definition.Type);

                    builder.RegisterInstance(_gameContext.Self)
                           .As<IGamePlayer>();
                    builder.RegisterInstance(_gameContext.Self.Hand);

                    // Скоуп локальной карты — ребёнок скоупа игрока, но граф проверяется от игрового скоупа:
                    // то, что карта берёт у игрока, регистрируется явно.
                    builder.RegisterInstance(_gameContext.Self.Mana);
                    builder.RegisterInstance(_gameContext.Self.Turns);
                    builder.RegisterInstance(_gameContext.Self.Modifiers);

                    builder.Register<HandEntryHandle>()
                           .As<IHandEntryHandle>();
                    builder.AddCardAction(_configs.Value, definition);

                    builder.RegisterInstance(definition);
                }
                else
                {
                    builder
                        .AddCardRemoteComponents()
                        .AddCardRemoteRoot()
                        .AddCardRemoteStates();

                    builder.RegisterInstance(definition.Type);
                    builder.RegisterInstance(gamePlayer)
                           .As<IGamePlayer>();
                    builder.RegisterInstance(gamePlayer.Hand);

                    builder.Register<HandEntryHandle>()
                           .WithParameter(gamePlayer.Hand)
                           .As<IHandEntryHandle>();

                    builder.RegisterInstance(definition);
                }
            }
        }
    }
}
