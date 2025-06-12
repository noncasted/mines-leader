using Cysharp.Threading.Tasks;
using Internal;
using Newtonsoft.Json;

namespace Global.Backend
{
    public static class BackendPostExtensions
    {
        public static UniTask<TResponse> PostJson<TResponse, TRequest>(
            this IBackendClient client,
            IReadOnlyLifetime lifetime,
            string uri,
            TRequest body)
        {
            var bodyJson = JsonConvert.SerializeObject(body);
            var request = new PostRequest(uri, bodyJson, new[] { RequestHeader.Json() });

            return client.Post.Post<TResponse>(lifetime, request);
        }

        public static UniTask PostJson<TBody>(
            this IBackendClient client,
            IReadOnlyLifetime lifetime,
            string uri,
            TBody body)
        {
            var bodyJson = JsonConvert.SerializeObject(body);
            var request = new PostRequest(uri, bodyJson, new[] { RequestHeader.Json() });

            return client.Post.Post(lifetime, request);
        }
    }
}