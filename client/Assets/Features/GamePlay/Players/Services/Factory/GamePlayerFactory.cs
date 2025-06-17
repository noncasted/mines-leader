using System.Collections.Generic;
using Assets.Meta;
using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using GamePlay.Services;
using Global.GameServices;
using Internal;
using VContainer.Unity;

namespace GamePlay.Players
{
    public class GamePlayerFactory : IGamePlayerFactory, IScopeSetup
    {
        public GamePlayerFactory(
            INetworkEntityFactory entityFactory,
            IEntityScopeLoader entityScopeLoader,
            IGlobalContext globalContext,
            IGameContext gameContext,
            IObjectFactory<GamePlayerEntityView> objectFactory,
            LifetimeScope parentScope,
            GamePlayerFactoryOptions options)
        {
            _entityFactory = entityFactory;
            _entityScopeLoader = entityScopeLoader;
            _globalContext = globalContext;
            _gameContext = gameContext;
            _objectFactory = objectFactory;
            _parentScope = parentScope;
            _options = options;
        }

        private readonly INetworkEntityFactory _entityFactory;
        private readonly IEntityScopeLoader _entityScopeLoader;
        private readonly IGlobalContext _globalContext;
        private readonly IGameContext _gameContext;
        private readonly IObjectFactory<GamePlayerEntityView> _objectFactory;
        private readonly LifetimeScope _parentScope;
        private readonly GamePlayerFactoryOptions _options;

        private readonly List<IGamePlayer> _remote = new();

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _entityFactory.ListenRemote<GamePlayerCreatePayload>(lifetime, OnRemote);
        }

        public async UniTask<IGamePlayer> CreateLocal(IReadOnlyLifetime lifetime)
        {
            var payload = new GamePlayerCreatePayload()
            {
                Id = _entityFactory.LocalUser.BackendId,
                Name = "Player_Local",
                SelectedCharacter = _globalContext.SelectedCharacter
            };

            var view = _objectFactory.Create(_options.LocalPrefab);
            var loadResult = await _entityScopeLoader.Load(lifetime, _parentScope, view, Build);

            var mana = loadResult.Get<IPlayerMana>();
            var health = loadResult.Get<IPlayerHealth>();
            var turns = loadResult.Get<IPlayerTurns>();

            mana.SetMax(_gameContext.Options.MaxMana);
            mana.SetCurrent(_gameContext.Options.StartMana);

            health.SetMax(_gameContext.Options.Health);
            health.SetCurrent(_gameContext.Options.Health);

            turns.SetMax(_gameContext.Options.Turns);
            turns.SetCurrent(_gameContext.Options.Turns);
            
            var entity = loadResult.Get<INetworkEntity>();
            var board = loadResult.Get<IBoard>();
            
            board.Setup(entity);
            
            await _entityFactory.Send(lifetime, entity, payload);

            await loadResult.Get<IEventLoop>().RunLoaded(loadResult.Lifetime);
            
            return loadResult.Get<IGamePlayer>();

            async UniTask Build(IEntityBuilder builder)
            {
                builder.AddLocalEntity(_entityFactory);
                
                var buildContext = new PlayerBuildContext(_gameContext, builder);

                await view.BoardFactory.CreateLocal(buildContext);
                await view.DeckFactory.CreateLocal(buildContext);
                await view.HandFactory.Create(buildContext);
                await view.StashFactory.CreateLocal(buildContext);
                await view.AvatarFactory.CreateLocal(buildContext);

                builder
                    .AddPlayerComponents()
                    .AddPlayerRoot(_entityFactory.LocalUser, _globalContext.SelectedCharacter);
            }
        }

        private async UniTask<INetworkEntity> OnRemote(IReadOnlyLifetime lifetime, RemoteEntityData data)
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

            return loadResult.Get<INetworkEntity>();

            async UniTask Build(IEntityBuilder builder)
            {
                builder.AddRemoteEntity(data);
                
                var buildContext = new PlayerBuildContext(_gameContext, builder);

                await view.BoardFactory.CreateRemote(buildContext);
                await view.DeckFactory.CreateRemote(buildContext);
                await view.HandFactory.Create(buildContext);
                await view.StashFactory.CreateRemote(buildContext);
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