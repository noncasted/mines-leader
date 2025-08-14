using Common.Network;
using Common.Objects;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using Internal;
using Shared;
using VContainer.Unity;

namespace GamePlay.Players
{
    public class GamePlayerFactory : IScopeSetup
    {
        public GamePlayerFactory(
            INetworkEntityFactory entityFactory,
            IEntityScopeLoader entityScopeLoader,
            IGameContext gameContext,
            IObjectFactory<GamePlayerEntityView> objectFactory,
            LifetimeScope parentScope,
            GamePlayerFactoryOptions options)
        {
            _entityFactory = entityFactory;
            _entityScopeLoader = entityScopeLoader;
            _gameContext = gameContext;
            _objectFactory = objectFactory;
            _parentScope = parentScope;
            _options = options;
        }

        private readonly INetworkEntityFactory _entityFactory;
        private readonly IEntityScopeLoader _entityScopeLoader;
        private readonly IGameContext _gameContext;
        private readonly IObjectFactory<GamePlayerEntityView> _objectFactory;
        private readonly LifetimeScope _parentScope;
        private readonly GamePlayerFactoryOptions _options;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _entityFactory.ListenRemote<PlayerCreatePayload>(lifetime, OnReceived);
        }

        private async UniTask<INetworkEntity> OnReceived(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            var payload = data.ReadPayload<PlayerCreatePayload>();

            var prefab = data.Owner.IsLocal == true ? _options.LocalPrefab : _options.RemotePrefab;
            var view = _objectFactory.Create(prefab);
            var loadResult = await _entityScopeLoader.Load(lifetime, _parentScope, view, Build);
            var player = loadResult.Get<IGamePlayer>();

            var board = loadResult.Get<IBoard>();
            var entity = loadResult.Get<INetworkEntity>();

            board.Setup(entity);

            loadResult.FillProperties(data);

            await loadResult.Get<IEventLoop>().RunLoaded(loadResult.Lifetime);
            _gameContext.AddPlayer(player);

            return loadResult.Get<INetworkEntity>();

            async UniTask Build(IEntityBuilder builder)
            {
                builder.AddRemoteEntity(data);

                var buildContext = new PlayerBuildContext(_gameContext, builder);

                await view.BoardFactory.Create(buildContext);
                await view.DeckFactory.Create(buildContext);
                await view.HandFactory.Create(buildContext);
                await view.StashFactory.Create(buildContext);
                await view.AvatarFactory.Create(buildContext);

                builder
                    .AddPlayerComponents()
                    .AddPlayerRoot(data.Owner, payload.SelectedCharacter);
            }
        }
    }
}