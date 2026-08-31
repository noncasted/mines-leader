using System;
using Cysharp.Threading.Tasks;
using Shared;
using UnityEngine;

namespace Internal
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

            Debug.Log($"[Network] [Session] User {userId} connecting to session {sessionId} at {serverUrl}");
            await _connection.Run(lifetime, serverUrl);
            Debug.Log($"[Network] [Session] Connection started successfully for user {userId}, authenticating...");

            var response = await _connection.Request<SharedSessionAuth.Response>(auth);

            Debug.Log(
                $"[Network] [Session] Authentication response received for user {userId} in session {sessionId}: Success = {response.IsSuccess}");

            if (response.IsSuccess == false)
            {
                Debug.LogError($"[Network] [Session] Failed to authenticate user {userId} for session {sessionId}");
                throw new Exception("Authentication failed");
            }
        }
    }
}