using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Global.Backend
{
    public class BackendMatchmaking :
        IBackendMatchmaking,
        IBackendProjection<MatchmakingContexts.GameResult>,
        IBackendProjection<MatchmakingContexts.LobbyResult>
    {
        public BackendMatchmaking(
            IBackendClient client,
            IBackendUser user,
            BackendOptions options)
        {
            _client = client;
            _user = user;
            _options = options;
        }

        private readonly IBackendClient _client;
        private readonly IBackendUser _user;
        private readonly BackendOptions _options;

        private UniTaskCompletionSource<SessionData> _gameCompletion;
        private UniTaskCompletionSource<SessionData> _lobbyCompletion;

        public UniTask OnReceived(MatchmakingContexts.GameResult data)
        {
            _gameCompletion.TrySetResult(new SessionData(data.ServerUrl, data.SessionId));
            return UniTask.CompletedTask;
        }

        public UniTask OnReceived(MatchmakingContexts.LobbyResult data)
        {
            _lobbyCompletion.TrySetResult(new SessionData(data.ServerUrl, data.SessionId));
            return UniTask.CompletedTask;
        }

        public async UniTask<SessionData> SearchGame(IReadOnlyLifetime lifetime)
        {
            _gameCompletion = new UniTaskCompletionSource<SessionData>();
            lifetime.Listen(() => _gameCompletion.TrySetCanceled());

            var url = _options.Url + MatchmakingContexts.SearchEndpoint;

            var request = new MatchmakingContexts.Search()
            {
                UserId = _user.Id,
                Type = MatchmakingConstants.GameType
            };

            await _client.PostJson(lifetime, url, request);

            return await _gameCompletion.Task;
        }

        public UniTask CancelSearch(IReadOnlyLifetime lifetime)
        {
            var url = _options.Url + MatchmakingContexts.CancelEndpoint;
            
            var request = new MatchmakingContexts.CancelSearch()
            {
                UserId = _user.Id
            };
            
            return _client.PostJson(lifetime, url, request);
        }

        public async UniTask<SessionData> CreateGame(IReadOnlyLifetime lifetime)
        {
            _gameCompletion = new UniTaskCompletionSource<SessionData>();
            lifetime.Listen(() => _gameCompletion.TrySetCanceled());

            var url = _options.Url + MatchmakingContexts.CreateEndpoint;

            var request = new MatchmakingContexts.Create()
            {
                UserId = _user.Id
            };

            await _client.PostJson(lifetime, url, request);

            return await _gameCompletion.Task;
        }

        public async UniTask<SessionData> SearchLobby(IReadOnlyLifetime lifetime)
        {
            _lobbyCompletion = new UniTaskCompletionSource<SessionData>();
            lifetime.Listen(() => _lobbyCompletion.TrySetCanceled());

            var url = _options.Url + MatchmakingContexts.SearchEndpoint;

            Debug.Log($"User {_user.Id} is searching for a lobby");
            
            var request = new MatchmakingContexts.Search()
            {
                UserId = _user.Id,
                Type = MatchmakingConstants.LobbyType
            };

            await _client.PostJson(lifetime, url, request);

            var data = await _lobbyCompletion.Task;
            
            Debug.Log($"Lobby found: {data.SessionId}");
            
            return data;
        }
    }
}