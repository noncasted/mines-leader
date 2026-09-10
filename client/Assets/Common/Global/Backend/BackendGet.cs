using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine.Networking;

namespace Global.Backend
{
    public interface IBackendGet
    {
        UniTask<string> Get(IReadOnlyLifetime lifetime, IGetRequest request);
    }

    public class BackendGet : IBackendGet
    {
        public async UniTask<string> Get(IReadOnlyLifetime lifetime, IGetRequest request)
        {
            // Запросы уходят из разных мест и живут параллельно, поэтому отрезок берётся
            // параллельным: он ложится в текущий этап, но чужие замеры в себя не втягивает.
            using var trace = GameProfiler.Concurrent($"GET {request.Uri}");

            using var downloadHandlerBuffer = new DownloadHandlerBuffer();
            using var webRequest = new UnityWebRequest(request.Uri, "GET", downloadHandlerBuffer, null);

            foreach (var header in request.Headers)
                webRequest.SetRequestHeader(header.Type, header.Value);

            await webRequest.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);

            if (webRequest.result != UnityWebRequest.Result.Success)
                throw new Exception("GET request failed");

            var responseContent = downloadHandlerBuffer.text;

            return responseContent;
        }
    }

    public static class BackendGetExtensions
    {
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