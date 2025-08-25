using System;
using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;
using Shared;
using UnityEngine;

namespace Common.Network
{
    public interface ISessionConnection
    {
        UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, Guid sessionId, Guid userId);
    }

    public class SessionConnection : ISessionConnection
    {
        public SessionConnection(INetworkConnection connection)
        {
            _connection = connection;
        }

        private readonly INetworkConnection _connection;

        public async UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, Guid sessionId, Guid userId)
        {
            var auth = new SharedSessionAuth.Request
            {
                SessionId = sessionId,
                UserId = userId
            };

            Debug.Log($"User {userId} connecting to session {sessionId} at {serverUrl}");

            await _connection.Run(lifetime, serverUrl);

            var response = await _connection.Request<SharedSessionAuth.Response>(auth);

            if (response.IsSuccess == false)
            {
                Debug.LogError($"Failed to authenticate user {userId} for session {sessionId}");
                throw new Exception("Authentication failed");
            }
        }
    }
}