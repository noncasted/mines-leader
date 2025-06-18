using Common.Network;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;
using VContainer.Unity;

namespace Menu.Social
{
    public interface IMenuPlayerFactory
    {
        UniTask Create(IReadOnlyLifetime lifetime);
    }

    public class MenuPlayerFactory : IMenuPlayerFactory, IScopeSetup
    {
        public MenuPlayerFactory(
            INetworkEntityFactory entityFactory,
            IUser user,
            IMenuPlayersCollection playersCollection,
            IEntityScopeLoader entityScopeLoader,
            IMenuPlayerObjectFactory objectFactory,
            LifetimeScope parentScope)
        {
            _entityFactory = entityFactory;
            _user = user;
            _playersCollection = playersCollection;
            _entityScopeLoader = entityScopeLoader;
            _objectFactory = objectFactory;
            _parentScope = parentScope;
        }

        private readonly INetworkEntityFactory _entityFactory;
        private readonly IUser _user;
        private readonly IMenuPlayersCollection _playersCollection;
        private readonly IEntityScopeLoader _entityScopeLoader;
        private readonly IMenuPlayerObjectFactory _objectFactory;
        private readonly LifetimeScope _parentScope;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _entityFactory.ListenRemote<MenuPlayerPayload>(lifetime, OnRemote);
        }

        public async UniTask Create(IReadOnlyLifetime lifetime)
        {
            var payload = new MenuPlayerPayload()
            {
                PlayerId = _user.Id
            };

            var view = _objectFactory.Create();
            var scope = await _entityScopeLoader.Load(lifetime, _parentScope, view, Build);
            var entity = scope.Get<INetworkEntity>();

            await _entityFactory.Send(lifetime, entity, payload);

            var player = scope.Get<IMenuPlayer>();
            _playersCollection.Add(player);
            void Build(IEntityBuilder builder)
            {
                builder.Register<MenuPlayer>()
                    .WithParameter(payload.PlayerId)
                    .WithScopeLifetime()
                    .As<IMenuPlayer>();

                builder.Register<MenuPlayerInput>()
                    .As<IMenuPlayerInput>()
                    .As<IScopeSetup>();

                builder.AddLocalEntity(_entityFactory);
                builder.RegisterProperty<MenuPlayerTransformState>();
            }
        }

        private async UniTask<INetworkEntity> OnRemote(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            var payload = data.ReadPayload<MenuPlayerPayload>();

            var view = _objectFactory.Create();
            var scope = await _entityScopeLoader.Load(lifetime, _parentScope, view, Build);
            var entity = scope.Get<INetworkEntity>();

            var player = scope.Get<IMenuPlayer>();
            _playersCollection.Add(player);

            return entity;

            void Build(IEntityBuilder builder)
            {
                builder.Register<MenuPlayer>()
                    .WithParameter(payload.PlayerId)
                    .WithScopeLifetime()
                    .As<IMenuPlayer>();

                builder.Register<MenuPlayerInput>()
                    .As<IMenuPlayerInput>()
                    .As<IScopeSetup>();

                builder.AddRemoteEntity(data);
                builder.RegisterProperty<MenuPlayerTransformState>();
            }
        }
    }
}