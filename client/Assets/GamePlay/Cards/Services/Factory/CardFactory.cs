using Common.Network;
using Common.Objects;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;
using VContainer.Unity;

namespace GamePlay.Cards
{
    public class CardFactory : IScopeSetup
    {
        public CardFactory(
            IEntityScopeLoader entityScopeLoader,
            INetworkEntityFactory entityFactory,
            IGameContext gameContext,
            ICardConfigs configs,
            IEnvDictionary<CardType, ICardDefinition> definitionsCollection,
            IObjectFactory<CardScopeEntity> objectFactory,
            LifetimeScope parentScope,
            CardFactoryOptions options)
        {
            _entityScopeLoader = entityScopeLoader;
            _gameContext = gameContext;
            _configs = configs;
            _definitionsCollection = definitionsCollection;
            _objectFactory = objectFactory;
            _entityFactory = entityFactory;
            _parentScope = parentScope;
            _options = options;
        }

        private readonly IEntityScopeLoader _entityScopeLoader;
        private readonly IGameContext _gameContext;
        private readonly ICardConfigs _configs;
        private readonly IEnvDictionary<CardType, ICardDefinition> _definitionsCollection;
        private readonly IObjectFactory<CardScopeEntity> _objectFactory;
        private readonly INetworkEntityFactory _entityFactory;
        private readonly LifetimeScope _parentScope;
        private readonly CardFactoryOptions _options;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _entityFactory.ListenRemote<CardCreatePayload>(lifetime, Create);
        }

        private async UniTask<INetworkEntity> Create(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            var payload = (CardCreatePayload)data.Payload;
            var gamePlayer = _gameContext.GetPlayer(payload.OwnerId);
            var definition = _definitionsCollection[payload.Type];

            var isLocal = data.Owner.IsLocal;
            var prefab = isLocal ? _options.LocalPrefab : _options.RemotePrefab;
            var parentScope = isLocal ? _gameContext.Self.Scope : _parentScope;
            var spawnPoint = isLocal ? _gameContext.Self.Deck.View.PickPoint : _gameContext.Other.Deck.View.PickPoint;

            var view = _objectFactory.Create(prefab, spawnPoint);
            var loadResult = await _entityScopeLoader.Load(lifetime, parentScope, view, Build);

            loadResult.FillProperties(data);

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
            
            return loadResult.Get<INetworkEntity>();

            void Build(IEntityBuilder builder)
            {
                if (isLocal == true)
                {
                    builder.AddLocalEntity(_entityFactory);

                    builder
                        .AddCardLocalComponents()
                        .AddCardLocalRoot()
                        .AddCardLocalStates();

                    builder.RegisterInstance(_configs.Value.All[definition.Type]);

                    builder.RegisterInstance(definition.Type);

                    builder.RegisterInstance(_gameContext.Self);
                    builder.RegisterInstance(_gameContext.Self.Hand);

                    builder.Register<HandEntryHandle>()
                        .As<IHandEntryHandle>();

                    builder.AddCardActionSync(definition);
                    builder.AddCardAction(_configs.Value, definition);

                    builder.RegisterInstance(definition);
                }
                else
                {
                    builder.AddRemoteEntity(data);

                    builder
                        .AddCardRemoteComponents()
                        .AddCardRemoteRoot()
                        .AddCardRemoteStates();

                    builder.AddCardActionSync(definition);

                    builder.RegisterInstance(definition.Type);
                    builder.RegisterInstance(gamePlayer);
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