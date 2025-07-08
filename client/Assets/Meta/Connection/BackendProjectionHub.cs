using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Meta
{
    public interface IBackendProjectionHub
    {
        UniTask Start(IReadOnlyLifetime lifetime);
    }

    public class BackendProjectionHub : IBackendProjectionHub
    {
        public BackendProjectionHub(
            IMetaBackend backend,
            IReadOnlyList<IBackendProjection> projections)
        {
            _projections = projections.ToDictionary(t => t.GetValueType());
            _backend = backend;
        }


        private readonly IMetaBackend _backend;
        private readonly Dictionary<Type, IBackendProjection> _projections;

        public UniTask Start(IReadOnlyLifetime lifetime)
        {
            _backend.Socket.Receiver.Empty.Advise(lifetime, OnUpdate);
            return UniTask.CompletedTask;
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