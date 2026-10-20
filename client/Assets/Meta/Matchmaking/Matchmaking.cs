using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Meta
{
    // A command that fails on the server throws from every method here (NetworkRequestException, see
    // IConnectionWriter.WriteRequest), as a lost connection does; the end of the lifetime cancels the wait.
    public interface IMatchmaking
    {
        // The result comes when the queue pairs the player, without a time limit. A match that fails to be created
        // from the queue is not reported: the server returns the player to the queue (N10 adds a failure result).
        UniTask<SharedMatchmaking.MatchResult> SearchGame(IReadOnlyLifetime lifetime, GameMatchType type);
        UniTask CancelSearch(IReadOnlyLifetime lifetime);

        // The server creates the session inside the command and sends the result before its response: no result in
        // ResultTimeout after the response - TimeoutException.
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
            IBackendProjection<SharedMatchmaking.MatchResult> gameResultProjection,
            IBackendProjection<SharedMatchmaking.LobbyResult> lobbyResultProjection)
        {
            _backend = backend;
            _metaConnection = metaConnection;
            _gameResultProjection = gameResultProjection;
            _lobbyResultProjection = lobbyResultProjection;
        }

        // The result is a separate message sent before the response to the command: without the limit a lost one
        // would hold the caller forever.
        private static readonly TimeSpan ResultTimeout = TimeSpan.FromSeconds(10);

        private readonly IMetaBackend _backend;
        private readonly IMetaConnection _metaConnection;
        private readonly IBackendProjection<SharedMatchmaking.MatchResult> _gameResultProjection;
        private readonly IBackendProjection<SharedMatchmaking.LobbyResult> _lobbyResultProjection;

        public async UniTask<SharedMatchmaking.MatchResult> SearchGame(IReadOnlyLifetime lifetime, GameMatchType type)
        {
            await _metaConnection.EnsureConnected(lifetime);
            var resultAwait = _gameResultProjection.WaitOnce(lifetime);
            await _backend.SearchGame(type);
            var result = await resultAwait;
            return result;
        }

        public UniTask CancelSearch(IReadOnlyLifetime lifetime)
        {
            return _backend.CancelSearch();
        }

        public UniTask<SharedMatchmaking.MatchResult> CreateGame(IReadOnlyLifetime lifetime)
        {
            return Request(lifetime, _gameResultProjection, () => _backend.CreateGame());
        }

        public UniTask<SharedMatchmaking.MatchResult> CreateGameWithBot(
            IReadOnlyLifetime lifetime,
            GameMatchType type,
            AgentMatchFixture fixture = null)
        {
            return Request(lifetime, _gameResultProjection, () => _backend.CreateGameWithBot(type, fixture));
        }

        public UniTask<SharedMatchmaking.LobbyResult> SearchLobby(IReadOnlyLifetime lifetime)
        {
            return Request(lifetime, _lobbyResultProjection, () => _backend.SearchLobby());
        }

        // Subscribed before the command: the result arrives before its response. A failed command throws here.
        private async UniTask<T> Request<T>(
            IReadOnlyLifetime lifetime,
            IBackendProjection<T> projection,
            Func<UniTask> command) where T : class, INetworkContext
        {
            await _metaConnection.EnsureConnected(lifetime);

            var requestLifetime = lifetime.Child();

            try
            {
                var resultAwait = projection.WaitOnce(requestLifetime);
                await command();

                var timeout = UniTask.Delay(ResultTimeout, DelayType.Realtime, cancellationToken: requestLifetime.Token);
                var (hasResult, result) = await UniTask.WhenAny(resultAwait, timeout);

                if (hasResult == false)
                    throw new TimeoutException(
                        $"[Matchmaking] No {typeof(T).Name} in {ResultTimeout.TotalSeconds} s after the command was accepted");

                return result;
            }
            finally
            {
                requestLifetime.Terminate();
            }
        }
    }
}
