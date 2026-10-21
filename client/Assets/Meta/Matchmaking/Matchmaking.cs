using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Meta
{
    // A command that fails on the server throws from every method here (NetworkRequestException, see
    // IConnectionWriter.WriteRequest), as a lost connection does; the end of the lifetime cancels the wait.
    public interface IMatchmaking
    {
        // The result comes when the queue pairs the player, without a time limit. A match the server failed to create
        // ends the search with an InvalidOperationException (MatchSearchStatus.Failed): the player is out of the queue.
        UniTask<SharedMatchmaking.MatchResult> SearchGame(IReadOnlyLifetime lifetime, GameMatchType type);
        UniTask CancelSearch(IReadOnlyLifetime lifetime);

        // The server creates the session inside the command, the result is its response.
        UniTask<SharedMatchmaking.LobbyResult> SearchLobby(IReadOnlyLifetime lifetime);
        UniTask<SharedMatchmaking.MatchResult> CreateGame(IReadOnlyLifetime lifetime);

        UniTask<SharedMatchmaking.MatchResult> CreateGameWithBot(
            IReadOnlyLifetime lifetime,
            GameMatchType type,
            AgentMatchFixture fixture = null);
    }

    // A meta lost since the menu was opened (a dropped network, a restarted server) is connected again before a
    // search: requests of the lost socket are never answered, and its search result would never come.
    public class Matchmaking : IMatchmaking
    {
        public Matchmaking(
            IMetaBackend backend,
            IMetaConnection metaConnection,
            IBackendProjection<SharedMatchmaking.MatchResult> gameResultProjection)
        {
            _backend = backend;
            _metaConnection = metaConnection;
            _gameResultProjection = gameResultProjection;
        }

        private readonly IMetaBackend _backend;
        private readonly IMetaConnection _metaConnection;
        private readonly IBackendProjection<SharedMatchmaking.MatchResult> _gameResultProjection;

        // N10: the search result is a projection, not a response. A result of an earlier search (cancelled while the
        // server was creating its match, replaced by this one) may arrive during this one: it carries another
        // RequestId and is dropped.
        public async UniTask<SharedMatchmaking.MatchResult> SearchGame(IReadOnlyLifetime lifetime, GameMatchType type)
        {
            await _metaConnection.EnsureConnected(lifetime);

            var requestId = Guid.NewGuid();
            var searchLifetime = lifetime.Child();
            var completion = new UniTaskCompletionSource<SharedMatchmaking.MatchResult>();

            try
            {
                searchLifetime.Listen(() => completion.TrySetCanceled());

                // Subscribed before the command: the result may come right after the response.
                _gameResultProjection.Advise(searchLifetime, OnResult);
                await _backend.SearchGame(type, requestId);

                var result = await completion.Task;

                if (result.Status != SharedMatchmaking.MatchSearchStatus.Matched)
                    throw new InvalidOperationException(
                        $"[Matchmaking] The server failed to create the {type} match of search {requestId}: {result.Status}");

                return result;
            }
            finally
            {
                searchLifetime.Terminate();
            }

            void OnResult(SharedMatchmaking.MatchResult result)
            {
                if (result.RequestId != requestId)
                {
                    Debug.LogWarning($"[Matchmaking] Ignored the {result.Status} result of search {result.RequestId}: "
                                     + $"waiting for search {requestId}");
                    return;
                }

                completion.TrySetResult(result);
            }
        }

        public UniTask CancelSearch(IReadOnlyLifetime lifetime)
        {
            return _backend.CancelSearch();
        }

        public async UniTask<SharedMatchmaking.MatchResult> CreateGame(IReadOnlyLifetime lifetime)
        {
            await _metaConnection.EnsureConnected(lifetime);
            return await _backend.CreateGame();
        }

        public async UniTask<SharedMatchmaking.MatchResult> CreateGameWithBot(
            IReadOnlyLifetime lifetime,
            GameMatchType type,
            AgentMatchFixture fixture = null)
        {
            await _metaConnection.EnsureConnected(lifetime);
            return await _backend.CreateGameWithBot(type, fixture);
        }

        public async UniTask<SharedMatchmaking.LobbyResult> SearchLobby(IReadOnlyLifetime lifetime)
        {
            await _metaConnection.EnsureConnected(lifetime);
            return await _backend.SearchLobby();
        }
    }
}
