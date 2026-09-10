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

    public class Matchmaking : IMatchmaking
    {
        public Matchmaking(
            IMetaBackend backend,
            IBackendProjection<SharedMatchmaking.MatchResult> gameResultProjection,
            IBackendProjection<SharedMatchmaking.LobbyResult> lobbyResultProjection)
        {
            _backend = backend;
            _gameResultProjection = gameResultProjection;
            _lobbyResultProjection = lobbyResultProjection;
        }

        private readonly IMetaBackend _backend;
        private readonly IBackendProjection<SharedMatchmaking.MatchResult> _gameResultProjection;
        private readonly IBackendProjection<SharedMatchmaking.LobbyResult> _lobbyResultProjection;

        public async UniTask<SharedMatchmaking.MatchResult> SearchGame(IReadOnlyLifetime lifetime, GameMatchType type)
        {
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
            var resultAwait = _gameResultProjection.WaitOnce(lifetime);
            await _backend.CreateGameWithBot(type, fixture);
            var result = await resultAwait;
            return result;
        }

        public async UniTask<SharedMatchmaking.LobbyResult> SearchLobby(IReadOnlyLifetime lifetime)
        {
            var resultAwait = _lobbyResultProjection.WaitOnce(lifetime);
            await _backend.SearchLobby();
            var result = await resultAwait;
            return result;
        }
    }
}