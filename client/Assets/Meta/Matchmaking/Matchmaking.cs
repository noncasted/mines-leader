using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;
using Shared;

namespace Meta
{
    public interface IMatchmaking
    {
        UniTask<SessionData> SearchGame(IReadOnlyLifetime lifetime);
        UniTask CancelSearch(IReadOnlyLifetime lifetime);
        UniTask<SessionData> SearchLobby(IReadOnlyLifetime lifetime);
        UniTask<SessionData> CreateGame(IReadOnlyLifetime lifetime);
    }

    public class Matchmaking : IMatchmaking
    {
        public Matchmaking(
            IMetaBackend backend,
            IBackendProjection<MatchmakingContexts.GameResult> gameResultProjection,
            IBackendProjection<MatchmakingContexts.LobbyResult> lobbyResultProjection)
        {
            _backend = backend;
            _gameResultProjection = gameResultProjection;
            _lobbyResultProjection = lobbyResultProjection;
        }

        private readonly IMetaBackend _backend;
        private readonly IBackendProjection<MatchmakingContexts.GameResult> _gameResultProjection;
        private readonly IBackendProjection<MatchmakingContexts.LobbyResult> _lobbyResultProjection;

        public async UniTask<SessionData> SearchGame(IReadOnlyLifetime lifetime)
        {
            var resultAwait = _gameResultProjection.WaitOnce(lifetime);
            await _backend.SearchGame();
            var result = await resultAwait;
            return new SessionData(result.ServerUrl, result.SessionId);
        }

        public UniTask CancelSearch(IReadOnlyLifetime lifetime)
        {
            return _backend.CancelSearch();
        }

        public async UniTask<SessionData> CreateGame(IReadOnlyLifetime lifetime)
        {
            var resultAwait = _gameResultProjection.WaitOnce(lifetime);
            await _backend.CreateGame();
            var result = await resultAwait;
            return new SessionData(result.ServerUrl, result.SessionId);
        }

        public async UniTask<SessionData> SearchLobby(IReadOnlyLifetime lifetime)
        {
            var resultAwait = _lobbyResultProjection.WaitOnce(lifetime);
            await _backend.SearchLobby();
            var result = await resultAwait;
            return new SessionData(result.ServerUrl, result.SessionId);
        }
    }
}