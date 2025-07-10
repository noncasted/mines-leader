using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Global.Backend
{
    public interface IBackendProjectionHub
    {
        UniTask Start(IReadOnlyLifetime lifetime, Guid userId);
    }

    public class BackendProjectionHub : IBackendProjectionHub
    {
        public BackendProjectionHub(
            IReadOnlyList<IBackendProjection> projections,
            BackendOptions options)
        {
            _projections = projections.ToDictionary(t => t.GetValueType());
            _options = options;
        }


        private readonly BackendOptions _options;
        private readonly Dictionary<Type, IBackendProjection> _projections;

        public async UniTask Start(IReadOnlyLifetime lifetime, Guid userId)
        {
            var socket = new NetworkSocket(_options.Url);
            await socket.Run(lifetime);

            var authResponse = await socket.SendFull<BackendConnectionAuth.Response>(new BackendConnectionAuth.Request()
            {
                UserId = userId
            });
            
            if (authResponse.IsSuccess == false)
                Debug.LogError($"[Projection] Failed to authenticate");
            
            socket.Receiver.Empty.Advise(lifetime, OnUpdate);
        }

        private void OnUpdate(ServerEmptyResponse response)
        {
            var type = response.Context.GetType();

            if (_projections.TryGetValue(type, out var projection) == false)
            {
                Debug.Log($"[Projection] No projection for type: {type.FullName}");
                return;
            }

            projection.OnReceived(response.Context);
        }
    }
}