using System;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Players;
using Internal;
using Tools;
using Meta;
using Shared;
using VContainer.Unity;

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
            LifetimeScope parentScope)
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
        private readonly LifetimeScope _parentScope;

        public async UniTask Create(IReadOnlyLifetime lifetime, bool isLocal, Guid cardId, CardType cardType)
        {
            var gamePlayer = isLocal ? _gameContext.Self : _gameContext.Other;
            var definition = _registry.Entries[cardType];

            var prefab = isLocal ? Prefabs.GamePlay.CardLocal : Prefabs.GamePlay.CardRemote;
            var parentScope = isLocal ? _gameContext.Self.Scope : _parentScope;
            var spawnPoint = isLocal ? _cardTargets.LocalSpawn : _cardTargets.RemoteSpawn;

            var view = _cardViewFactory.Create(prefab, spawnPoint);
            var loadResult = await _entityScopeLoader.Load(lifetime, parentScope, view, Build);

            if (isLocal == true)
            {
                var spawn = loadResult.Get<ICardLocalSpawn>();
                await spawn.Execute();
            }
            else
            {
                var spawn = loadResult.Get<ICardRemoteSpawn>();
                await spawn.Execute();
            }

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

                    builder.Register<HandEntryHandle>()
                           .As<IHandEntryHandle>();

                    builder.AddCardActionSync(definition);
                    builder.AddCardAction(_configs.Value, definition);

                    builder.RegisterInstance(definition);
                }
                else
                {
                    builder
                        .AddCardRemoteComponents()
                        .AddCardRemoteRoot()
                        .AddCardRemoteStates();

                    builder.AddCardActionSync(definition);

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
