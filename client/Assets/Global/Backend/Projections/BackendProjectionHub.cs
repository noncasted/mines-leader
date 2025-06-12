using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using Microsoft.AspNetCore.SignalR.Client;
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
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_options.Url}/observer?UserId={userId}")
                .WithAutomaticReconnect()
                .Build();

            connection.On<byte[]>("Update", OnUpdate);
            await connection.StartAsync();

            lifetime.Listen(() => connection.DisposeAsync());
        }

        private void OnUpdate(byte[] raw)
        {
            var context = MemoryPackSerializer.Deserialize<INetworkContext>(raw);
            var type = context.GetType();

            if (_projections.TryGetValue(type, out var projection) == false)
            {
                Debug.Log($"[Projection] No projection for type: {type.FullName}");
                return;
            }

            projection.OnReceived(context);
        }
    }
}