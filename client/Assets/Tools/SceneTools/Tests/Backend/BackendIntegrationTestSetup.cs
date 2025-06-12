using System;
using System.Collections.Generic;
using Common.Network;
using Common.Network.Common;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Backend;
using Global.GameServices;
using Internal;
using MemoryPack;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Tools
{
    public class BackendIntegrationTestSetup : MockBase
    {
        [SerializeField] private BackendOptions _backendOptions;
        
        private ILoadedScope _globalScope;
        private ILoadedScope _testScope;
//        private NetworkProperty<int> _property;

        public override async UniTaskVoid Process()
        {
            _globalScope = await BootstrapGlobal();
            var loader = _globalScope.Get<IServiceScopeLoader>();
            await _testScope.Initialize();
            
            UniTask Construct(IScopeBuilder builder)
            {
                builder.AddSessionServices();
                return UniTask.CompletedTask;
            }
        }

        [Button("Connect")]
        private async void Connect()
        {
            var userContext = _testScope.Get<IUserContext>();
            var backendHub = _testScope.Get<IBackendProjectionHub>();
            var matchmaking = _testScope.Get<IBackendMatchmaking>();
            var session = _testScope.Get<INetworkSession>();
            var entityIds = _testScope.Get<INetworkEntityIds>();
            var users = _testScope.Get<INetworkUsersCollection>();
            var entityFactory = _testScope.Get<INetworkEntityFactory>();
            var lifetime = this.GetObjectLifetime();

            await userContext.Init(_testScope.Lifetime);
            await backendHub.Start(_testScope.Lifetime, userContext.Id);

            await UniTask.Delay(TimeSpan.FromSeconds(1));

            var result = await matchmaking.SearchGame(lifetime);
            var serverIP = $"ws://{result.ServerUrl}";

            await session.Start(lifetime, serverIP, result.SessionId);

            entityFactory.ListenRemote<TestEntityPayload>(lifetime, OnRemote);

            var properties = new List<INetworkProperty>();
            // var entity = new NetworkEntity(users.Local, entityIds.GetEntityId(), properties);
            // _property = new NetworkProperty<int>(entity);
            // _property.Set(10);
            // properties.Add(_property);
            //
            // await entityFactory.Send(lifetime, entity, new TestEntityPayload());
           
            Debug.Log($"SessionId: {result.SessionId}");
        }

        [Button]
        private void Inc()
        {
//            _property.Set(_property.Value + 1);
        }

        private async UniTask<INetworkEntity> OnRemote(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            // var properties = new List<INetworkProperty>();
            // var entity = new NetworkEntity(data.Owner, data.Id, properties);
            //
            // var property = new NetworkProperty<int>(entity);
            // property.Set(10);
            // property.Update(data.RawProperties[0]);
            // properties.Add(property);
            //
            // property.View(lifetime, value => Debug.Log($"Remote: {value}"));
            //
            // return entity;
            return null;
        }
            
    }

    [MemoryPackable]
    public partial class TestEntityPayload : IEntityPayload
    {
        
    }
}