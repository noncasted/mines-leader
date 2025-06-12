using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using Microsoft.AspNetCore.SignalR.Client;
using Shared;
using UnityEngine;

namespace Global.Backend
{
    public class BackendProjectionHub : IBackendProjectionHub
    {
        public BackendProjectionHub(BackendOptions options)
        {
            _options = options;
        }

        private readonly BackendOptions _options;
        private readonly Dictionary<Type, Action<INetworkContext>> _listeners = new();

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

        public void AddListener<T>(IBackendProjection<T> projection) where T : INetworkContext
        {
            var type = typeof(T);
            _listeners.Add(type, context => projection.OnReceived((T)context));
        }

        private void OnUpdate(byte[] raw)
        {
            var context = MemoryPackSerializer.Deserialize<INetworkContext>(raw);
            var type = context.GetType();

            if (_listeners.TryGetValue(type, out var listener) == false)
            {
                Debug.Log($"[Projection] No projection listener for type: {type.FullName}");
                return;
            }

            UniTask.Create(async () =>
            {
                await UniTask.SwitchToMainThread();
                listener(context);
            });
        }
    }
}