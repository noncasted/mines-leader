using Cysharp.Threading.Tasks;
using Internal;
using Newtonsoft.Json;

namespace Global.Backend
{
    public static class BackendGetExtensions
    {
        public static async UniTask<T> Get<T>(
            this IBackendClient client,
            IReadOnlyLifetime lifetime,
            string uri,
            params IRequestHeader[] headers)
        {
            var raw = await client.Get(lifetime, uri, headers);
            var result = JsonConvert.DeserializeObject<T>(raw);

            return result;
        }

        public static UniTask<string> Get(
            this IBackendClient client,
            IReadOnlyLifetime lifetime,
            string uri,
            params IRequestHeader[] headers)
        {
            var request = new GetRequest(uri, headers);

            return client.Get.Get(lifetime, request);
        }
    }
}