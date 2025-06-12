using Common.Network;
using Common.Network.Common;
using Cysharp.Threading.Tasks;
using Global.GameServices;
using Global.Systems;
using Internal;
using UnityEngine;
using VContainer;

namespace Menu
{
    [DisallowMultipleComponent]
    public class MenuPlayerFactory : MonoBehaviour, IMenuPlayerFactory, ISceneService, IScopeSetup
    {
        [SerializeField] private MenuPlayerOptions _options;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private float _spawnRadius = 1f;

        private INetworkEntityFactory _entityFactory;
        private INetworkEntityDestroyer _destroyer;
        private IUserContext _userContext;
        private IUpdater _updater;
        private IMenuPlayersCollection _playersCollection;
        private INetworkSender _sender;

        [Inject]
        private void Construct(
            INetworkSender sender,
            INetworkEntityFactory entityFactory,
            INetworkEntityDestroyer destroyer,
            IUserContext userContext,
            IUpdater updater,
            IMenuPlayersCollection playersCollection)
        {
            _sender = sender;
            _playersCollection = playersCollection;
            _updater = updater;
            _userContext = userContext;
            _destroyer = destroyer;
            _entityFactory = entityFactory;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IMenuPlayerFactory>()
                .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _entityFactory.ListenRemote<MenuPlayerPayload>(lifetime, OnRemote);
        }

        public async UniTask Create(IReadOnlyLifetime lifetime)
        {
            await UniTask.Yield();

            var properties = new NetworkObjectProperties();

            var entity = new NetworkEntity(
                _sender,
                _destroyer,
                _entityFactory.LocalUser,
                _entityFactory.Ids.GetEntityId(),
                properties);

            var spawnPosition = (Vector2)_spawnPoint.position + RandomExtensions.RandomDirection() * _spawnRadius;
            var player = Instantiate(_options.Prefab, spawnPosition, Quaternion.identity, transform);

            var input = new MenuPlayerInput();
            input.Setup(entity.Lifetime);

            var transformState = new NetworkProperty<MenuPlayerTransformState>(0);
            properties.Add(transformState);

            player.Movement.Construct(_updater, input, entity, transformState);
            player.Movement.Setup(entity.Lifetime);

            player.Construct(_userContext.Id, entity.Lifetime);
            _playersCollection.Add(player);

            var payload = new MenuPlayerPayload()
            {
                PlayerId = _userContext.Id
            };

            await _entityFactory.Send(lifetime, entity, payload);
        }

        private async UniTask<INetworkEntity> OnRemote(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            var payload = data.ReadPayload<MenuPlayerPayload>();
            var properties = new NetworkObjectProperties();

            var entity = new NetworkEntity(
                _sender,
                _destroyer,
                data.Owner,
                data.Id,
                properties);

            var spawnPosition = (Vector2)_spawnPoint.position + RandomExtensions.RandomDirection() * _spawnRadius;
            var player = Instantiate(_options.Prefab, spawnPosition, Quaternion.identity, transform);

            var input = new MenuPlayerInput();
            input.Setup(entity.Lifetime);

            var transformState = new NetworkProperty<MenuPlayerTransformState>(0);
            properties.Add(transformState);

            player.Movement.Construct(_updater, input, entity, transformState);
            player.Movement.Setup(entity.Lifetime);

            player.Construct(payload.PlayerId, entity.Lifetime);
            _playersCollection.Add(player);

            entity.Lifetime.Listen(() => Destroy(player.gameObject));

            return entity;
        }
    }
}