using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine.Networking;

namespace Global.Backend
{
    public class BackendGetGateway : IBackendGetGateway
    {
        public async UniTask<string> Get(IReadOnlyLifetime lifetime, IGetRequest request)
        {
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
}