using Cysharp.Threading.Tasks;
using Global.Backend;
using Shared;
using UnityEngine;

namespace Meta
{
    public static class BackendEndpoints
    {
        public static UniTask<SharedBackendUserAuth.Response> Auth(this IMetaBackend backend, string name)
        {
            return backend.Post<SharedBackendUserAuth.Response, SharedBackendUserAuth.Request>(
                SharedBackendUserAuth.Endpoint,
                new SharedBackendUserAuth.Request()
                {
                    Name = name
                });
        }

        public static UniTask SearchGame(this IMetaBackend backend)
        {
            return backend.ExecuteCommand(new SharedMatchmaking.Search()
            {
                Type = SharedMatchmaking.GameType
            });
        }

        public static UniTask CancelSearch(this IMetaBackend backend)
        {
            return backend.ExecuteCommand(new SharedMatchmaking.CancelSearch());
        }

        public static UniTask CreateGame(this IMetaBackend backend)
        {
            return backend.ExecuteCommand(new SharedMatchmaking.Create());
        }

        public static UniTask SearchLobby(this IMetaBackend backend)
        {
            return backend.ExecuteCommand(new SharedMatchmaking.Search()
            {
                Type = SharedMatchmaking.LobbyType
            });
        }

        public static async UniTask ExecuteCommand<TRequest>(this IMetaBackend backend, TRequest request)
            where TRequest : INetworkContext
        {
            var response = await backend.Connection.Writer.WriteRequest<EmptyResponse>(request);

            if (response.HasError == true)
                Debug.LogError($"Request {typeof(TRequest).Name} executed with error");
        }

        private static UniTask<TResponse> Post<TResponse, TRequest>(
            this IMetaBackend backend,
            string uri,
            TRequest body)
        {
            var backendUrl = backend.Client.Options.Url + uri;
            return backend.Client.PostJson<TResponse, TRequest>(backend.Lifetime, backendUrl, body);
        }
    }
}