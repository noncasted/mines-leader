using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Internal;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Global.Backend
{
    public interface IBackendPost
    {
        UniTask<T> Post<T>(IReadOnlyLifetime lifetime, IPostRequest request);
        UniTask Post(IReadOnlyLifetime lifetime, IPostRequest request);
    }
    
    public class BackendPost : IBackendPost
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