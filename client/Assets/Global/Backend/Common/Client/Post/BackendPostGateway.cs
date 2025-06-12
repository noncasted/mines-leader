using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Internal;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Global.Backend
{
    public class BackendPostGateway : IBackendPostGateway
    {
        public async UniTask<T> Post<T>(IReadOnlyLifetime lifetime, IPostRequest request)
        {
            using var downloadHandlerBuffer = new DownloadHandlerBuffer();
            UploadHandlerRaw uploadHandler = null;

            if (request.Body != null)
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.Body));

            using var webRequest = new UnityWebRequest(request.Uri, "POST", downloadHandlerBuffer, uploadHandler);

            foreach (var header in request.Headers)
                webRequest.SetRequestHeader(header.Type, header.Value);

            Debug.Log(request.Uri);
            await webRequest.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);

            if (webRequest.result != UnityWebRequest.Result.Success)
                throw new Exception("POST request failed");

            var responseContent = downloadHandlerBuffer.text;
            var result = JsonConvert.DeserializeObject<T>(responseContent);

            uploadHandler?.Dispose();

            return result;
        }

        public async UniTask Post(IReadOnlyLifetime lifetime, IPostRequest request)
        {
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