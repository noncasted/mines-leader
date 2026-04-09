using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using Internal;
using Network;
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
            LifetimeScope parentScope,
            LocalPlayerView local,
            RemotePlayerView remote)
        {
            _entityFactory = entityFactory;
            _entityScopeLoader = entityScopeLoader;
            _gameContext = gameContext;
            _parentScope = parentScope;
            _local = local;
            _remote = remote;
        }

        private readonly INetworkEntityFactory _entityFactory;
        private readonly IEntityScopeLoader _entityScopeLoader;
        private readonly IGameContext _gameContext;
        private readonly LifetimeScope _parentScope;
        private readonly LocalPlayerView _local;
        private readonly RemotePlayerView _remote;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _entityFactory.ListenRemote<PlayerCreatePayload>(lifetime, OnReceived);
        }

        private async UniTask<INetworkEntity> OnReceived(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            var payload = (PlayerCreatePayload)data.Payload;

            ScopeEntityView view = data.Owner.IsLocal ? _local : _remote;
            var loadResult = await _entityScopeLoader.Load(lifetime, _parentScope, view, Build);
            var player = loadResult.Get<IGamePlayer>();

            var board = loadResult.Get<IBoard>();
            var entity = loadResult.Get<INetworkEntity>();

            board.Setup(entity);

            loadResult.FillProperties(data);

            await loadResult.Get<IEventLoop>().RunLoaded(loadResult.Lifetime);
            _gameContext.AddPlayer(player);

            return loadResult.Get<INetworkEntity>();


            void Build(IEntityBuilder builder)
            {
                builder.AddRemoteEntity(data);

                builder
                    .AddPlayerComponents()
                    .AddPlayerRoot(data.Owner, payload.SelectedCharacter);

                builder.RegisterProperty<BoardState>(PlayerStateIds.Board);
                builder.RegisterProperty<PlayerManaState>(PlayerStateIds.Mana);
                builder.RegisterProperty<PlayerHealthState>(PlayerStateIds.Health);
                builder.RegisterProperty<PlayerMovesState>(PlayerStateIds.Moves);
                builder.RegisterProperty<PlayerModifiersState>(PlayerStateIds.Modifiers);
                builder.RegisterProperty<PlayerHandState>(PlayerStateIds.Hand);
                builder.RegisterProperty<PlayerStashState>(PlayerStateIds.Stash);
                builder.RegisterProperty<PlayerDeckState>(PlayerStateIds.Deck);
            }
        }
    }
}