using System.Collections.Generic;
using Common.Network;
using Common.Objects;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using Internal;
using Meta;
using VContainer.Unity;

namespace GamePlay.Players
{
    public class GamePlayerFactory : IScopeSetup
    {
        public GamePlayerFactory(
            INetworkEntityFactory entityFactory,
            IEntityScopeLoader entityScopeLoader,
            IGameContext gameContext,
            IUser user,
            IObjectFactory<GamePlayerEntityView> objectFactory,
            LifetimeScope parentScope,
            GamePlayerFactoryOptions options)
        {
            _entityFactory = entityFactory;
            _entityScopeLoader = entityScopeLoader;
            _gameContext = gameContext;
            _user = user;
            _objectFactory = objectFactory;
            _parentScope = parentScope;
            _options = options;
        }

        private readonly INetworkEntityFactory _entityFactory;
        private readonly IEntityScopeLoader _entityScopeLoader;
        private readonly IGameContext _gameContext;
        private readonly IUser _user;
        private readonly IObjectFactory<GamePlayerEntityView> _objectFactory;
        private readonly LifetimeScope _parentScope;
        private readonly GamePlayerFactoryOptions _options;

        private readonly List<IGamePlayer> _remote = new();

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _entityFactory.ListenRemote<GamePlayerCreatePayload>(lifetime, OnReceived);
        }

        private UniTask<INetworkEntity> OnReceived(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            if (data.Owner.IsLocal == true)
                return CreateLocal(lifetime, data);

            return CreateRemote(lifetime, data);
        }

        private async UniTask<INetworkEntity> CreateLocal(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            var payload = data.ReadPayload<GamePlayerCreatePayload>();

            var view = _objectFactory.Create(_options.RemotePrefab);
            var loadResult = await _entityScopeLoader.Load(lifetime, _parentScope, view, Build);
            var player = loadResult.Get<IGamePlayer>();

            var board = loadResult.Get<IBoard>();
            var entity = loadResult.Get<INetworkEntity>();

            board.Setup(entity);

            loadResult.FillProperties(data);
            _remote.Add(player);

            await loadResult.Get<IEventLoop>().RunLoaded(loadResult.Lifetime);
            _gameContext.AddPlayer(player);

            return loadResult.Get<INetworkEntity>();

            async UniTask Build(IEntityBuilder builder)
            {
                builder.AddRemoteEntity(data);

                var buildContext = new PlayerBuildContext(_gameContext, builder);

                await view.BoardFactory.CreateLocal(buildContext);
                await view.DeckFactory.Create(buildContext);
                await view.HandFactory.Create(buildContext);
                await view.StashFactory.Create(buildContext);
                await view.AvatarFactory.CreateLocal(buildContext);

                builder
                    .AddPlayerComponents()
                    .AddPlayerRoot(data.Owner, payload.SelectedCharacter);
            }
        }

        private async UniTask<INetworkEntity> CreateRemote(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            var payload = data.ReadPayload<GamePlayerCreatePayload>();

            var view = _objectFactory.Create(_options.RemotePrefab);
            var loadResult = await _entityScopeLoader.Load(lifetime, _parentScope, view, Build);
            var player = loadResult.Get<IGamePlayer>();

            var board = loadResult.Get<IBoard>();
            var entity = loadResult.Get<INetworkEntity>();

            board.Setup(entity);

            loadResult.FillProperties(data);
            _remote.Add(player);

            await loadResult.Get<IEventLoop>().RunLoaded(loadResult.Lifetime);
            _gameContext.AddPlayer(player);
            
            return loadResult.Get<INetworkEntity>();

            async UniTask Build(IEntityBuilder builder)
            {
                builder.AddRemoteEntity(data);

                var buildContext = new PlayerBuildContext(_gameContext, builder);

                await view.BoardFactory.CreateRemote(buildContext);
                await view.DeckFactory.Create(buildContext);
                await view.HandFactory.Create(buildContext);
                await view.StashFactory.Create(buildContext);
                await view.AvatarFactory.CreateRemote(buildContext);

                builder
                    .AddPlayerComponents()
                    .AddPlayerRoot(data.Owner, payload.SelectedCharacter);
            }
        }

        public async UniTask<IReadOnlyList<IGamePlayer>> WaitRemote(IReadOnlyLifetime lifetime, int remoteAmount)
        {
            await UniTask.WaitUntil(() => _remote.Count == remoteAmount, cancellationToken: lifetime.Token);
            return _remote;
        }
    }
}