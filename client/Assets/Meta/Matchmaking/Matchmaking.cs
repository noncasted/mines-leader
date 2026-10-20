using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Meta
{
    public interface IMatchmaking
    {
        UniTask<SharedMatchmaking.MatchResult> SearchGame(IReadOnlyLifetime lifetime, GameMatchType type);
        UniTask CancelSearch(IReadOnlyLifetime lifetime);
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

        public async UniTask<SharedMatchmaking.MatchResult> CreateGame(IReadOnlyLifetime lifetime)
        {
            await _metaConnection.EnsureConnected(lifetime);
            var resultAwait = _gameResultProjection.WaitOnce(lifetime);
            await _backend.CreateGame();
            var result = await resultAwait;
            return result;
        }

        public async UniTask<SharedMatchmaking.MatchResult> CreateGameWithBot(
            IReadOnlyLifetime lifetime,
            GameMatchType type,
            AgentMatchFixture fixture = null)
        {
            await _metaConnection.EnsureConnected(lifetime);
            var resultAwait = _gameResultProjection.WaitOnce(lifetime);
            await _backend.CreateGameWithBot(type, fixture);
            var result = await resultAwait;
            return result;
        }

        public async UniTask<SharedMatchmaking.LobbyResult> SearchLobby(IReadOnlyLifetime lifetime)
        {
            await _metaConnection.EnsureConnected(lifetime);
            var resultAwait = _lobbyResultProjection.WaitOnce(lifetime);
            await _backend.SearchLobby();
            var result = await resultAwait;
            return result;
        }
    }
}