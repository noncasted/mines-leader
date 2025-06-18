using Cysharp.Threading.Tasks;
using Global.Backend;
using Shared;

namespace Meta
{
    public static class BackendEndpoints
    {
        public static UniTask<BackendAuthContexts.Response> Auth(this IMetaBackend backend, string name)
        {
            return backend.Post<BackendAuthContexts.Response, BackendAuthContexts.Request>(
                BackendAuthContexts.Endpoint,
                new BackendAuthContexts.Request()
                {
                    Name = name
                });
        }

        public static UniTask SearchGame(this IMetaBackend backend)
        {
            return backend.Post(MatchmakingContexts.SearchEndpoint, new MatchmakingContexts.Search()
            {
                UserId = backend.User.Id,
                Type = MatchmakingConstants.GameType
            });
        }

        public static UniTask CancelSearch(this IMetaBackend backend)
        {
            return backend.Post(MatchmakingContexts.CancelEndpoint, new MatchmakingContexts.CancelSearch()
            {
                UserId = backend.User.Id,
            });
        }

        public static UniTask CreateGame(this IMetaBackend client)
        {
            return client.Post(MatchmakingContexts.CreateEndpoint, new MatchmakingContexts.Create()
            {
                UserId = client.User.Id,
            });
        }

        public static UniTask SearchLobby(this IMetaBackend client)
        {
            return client.Post(MatchmakingContexts.SearchEndpoint, new MatchmakingContexts.Search()
            {
                UserId = client.User.Id,
                Type = MatchmakingConstants.LobbyType
            });
        }

        private static UniTask<TResponse> Post<TResponse, TRequest>(
            this IMetaBackend backend,
            string uri,
            TRequest body)
        {
            var backendUrl = backend.Client.Options.Url + uri;
            return backend.Client.PostJson<TResponse, TRequest>(backend.Lifetime, backendUrl, body);
        }

        private static UniTask Post<TRequest>(
            this IMetaBackend backend,
            string uri,
            TRequest body)
        {
            var backendUrl = backend.Client.Options.Url + uri;
            return backend.Client.PostJson(backend.Lifetime, backendUrl, body);
        }
    }
}