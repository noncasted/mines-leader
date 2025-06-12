using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.GameServices;
using Internal;
using Shared;
using UnityEngine;
using VContainer.Unity;

namespace GamePlay.Cards
{
    public class CardFactory : ICardFactory, IScopeSetup
    {
        public CardFactory(
            IEntityScopeLoader entityScopeLoader,
            INetworkEntityFactory entityFactory,
            IGameContext gameContext,
            IEnvDictionary<CardType, ICardDefinition> definitionsCollection,
            LifetimeScope parentScope,
            CardFactoryOptions options)
        {
            _entityScopeLoader = entityScopeLoader;
            _gameContext = gameContext;
            _definitionsCollection = definitionsCollection;
            _entityFactory = entityFactory;
            _parentScope = parentScope;
            _options = options;
        }

        private readonly IEntityScopeLoader _entityScopeLoader;
        private readonly IGameContext _gameContext;
        private readonly IEnvDictionary<CardType, ICardDefinition> _definitionsCollection;
        private readonly INetworkEntityFactory _entityFactory;
        private readonly LifetimeScope _parentScope;
        private readonly CardFactoryOptions _options;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _entityFactory.ListenRemote<CardCreatePayload>(lifetime, OnRemote);
        }

        public async UniTask Create(IReadOnlyLifetime lifetime, CardType type, Vector2 position)
        {
            var definition = _definitionsCollection[type];

            var payload = new CardCreatePayload()
            {
                Type = definition.Type,
                OwnerId = _gameContext.Self.Id,
                SpawnPoint = position
            };

            var view = Object.Instantiate(_options.LocalPrefab, position, Quaternion.identity);

            var parentScope = _gameContext.Self.Scope;
            var loadResult = await _entityScopeLoader.Load(lifetime, parentScope, view, Build);
            var entity = loadResult.Get<INetworkEntity>();
            await _entityFactory.Send(lifetime, entity, payload);

            var spawn = loadResult.Get<ICardLocalSpawn>();
            await spawn.Execute();

            return;

            void Build(IEntityBuilder builder)
            {
                builder.AddLocalEntity(_entityFactory);

                builder
                    .AddCardLocalComponents()
                    .AddCardLocalRoot()
                    .AddCardLocalStates();

                builder.RegisterInstance(definition.Type);
                builder.RegisterInstance(definition.Target);

                builder.RegisterInstance(_gameContext.Self);
                builder.RegisterInstance(_gameContext.Self.Hand);
                builder.RegisterInstance(_gameContext.Self.Stash);

                builder.Register<HandEntryHandle>()
                    .As<IHandEntryHandle>();

                builder.AddCardAction(definition);

                builder.RegisterInstance(definition);
            }
        }

        private async UniTask<INetworkEntity> OnRemote(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            var payload = data.ReadPayload<CardCreatePayload>();
            var gamePlayer = _gameContext.GetPlayer(payload.OwnerId);
            var definition = _definitionsCollection[payload.Type];

            var view = Object.Instantiate(_options.RemotePrefab, payload.SpawnPoint, Quaternion.identity);
            var loadResult = await _entityScopeLoader.Load(lifetime, _parentScope, view, Build);

            loadResult.FillProperties(data);

            var spawn = loadResult.Get<ICardRemoteSpawn>();
            await spawn.Execute();

            return loadResult.Get<INetworkEntity>();

            void Build(IEntityBuilder builder)
            {
                builder.AddRemoteEntity(data);

                builder
                    .AddCardRemoteComponents()
                    .AddCardRemoteRoot()
                    .AddCardRemoteStates();


                builder.RegisterInstance(definition.Type);
                builder.RegisterInstance(definition.Target);
                builder.RegisterInstance(gamePlayer);
                builder.RegisterInstance(gamePlayer.Hand);

                builder.Register<HandEntryHandle>()
                    .WithParameter(gamePlayer.Hand)
                    .As<IHandEntryHandle>();

                builder.AddCardAction(definition);

                builder.RegisterInstance(definition);
            }
        }
    }
}