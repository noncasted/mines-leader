using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine.Networking;

namespace Global.Backend
{
    public interface IBackendPost
    {
        UniTask Post(IReadOnlyLifetime lifetime, IPostRequest request);
    }

    public class BackendPost : IBackendPost
    {
        public async UniTask Post(IReadOnlyLifetime lifetime, IPostRequest request)
        {
            using var trace = GameProfiler.Concurrent($"POST {request.Uri}");

            using var downloadHandlerBuffer = new DownloadHandlerBuffer();
            UploadHandlerRaw uploadHandler = null;

            if (request.Body != null)
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.Body));

            using var webRequest = new UnityWebRequest(request.Uri, "POST", null, uploadHandler);

            foreach (var header in request.Headers)
                webRequest.SetRequestHeader(header.Type, header.Value);

            await webRequest.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);

            if (webRequest.result != UnityWebRequest.Result.Success)
                throw new Exception("POST request failed");
        }
    }
}
