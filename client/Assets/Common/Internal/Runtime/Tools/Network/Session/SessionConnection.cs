using System;
using Cysharp.Threading.Tasks;
using Shared;
using UnityEngine;

namespace Internal
{
    public interface ISessionConnection
    {
        UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, string ticket);
    }

    public class SessionConnection : ISessionConnection
    {
        public SessionConnection(INetworkConnection connection)
        {
            _connection = connection;
        }

        private readonly INetworkConnection _connection;

        // S4: the first frame of the socket is the ticket; a refusal throws (the server closes the socket after it).
        public async UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, string ticket)
        {
            if (string.IsNullOrEmpty(ticket) == true)
                throw new ArgumentException("[Network] [Session] A session is entered without a ticket", nameof(ticket));

            var auth = new SharedSessionAuth.Request
            {
                Ticket = ticket
            };

            Debug.Log($"[Network] [Session] Connecting to the session at {serverUrl}");
            await _connection.Run(lifetime, serverUrl);
            Debug.Log("[Network] [Session] Connection started, authenticating...");

            // No answer throws: OperationCanceledException when the socket was lost before it, TimeoutException,
            // NetworkRequestException when the session failed to handle it (IConnectionWriter.WriteRequest).
            var response = await _connection.Request<SharedSessionAuth.Response>(auth);

            Debug.Log($"[Network] [Session] Authentication response: {response.Status}");

            if (response.Status != SessionAuthStatus.Ok)
                throw new InvalidOperationException($"[Network] [Session] The session refused the entry: {response.Status}");
        }
    }
}
