using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;
using UnityEngine.Networking;

namespace Global.Backend
{
    public class BackendMediaGateway : IBackendMediaGateway
    {
        public async UniTask<AudioClip> GetAudio(IReadOnlyLifetime lifetime, IGetRequest request, AudioType audioType)
        {
            using var downloadHandlerAudioClip = new DownloadHandlerAudioClip(request.Uri, audioType);
            using var webRequest = new UnityWebRequest(request.Uri, "GET", downloadHandlerAudioClip, null);

            foreach (var header in request.Headers)
                webRequest.SetRequestHeader(header.Type, header.Value);

            await webRequest.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);

            if (webRequest.result != UnityWebRequest.Result.Success)
                throw new Exception("GET request failed");

            return downloadHandlerAudioClip.audioClip;
        }

        public async UniTask<Texture2D> GetImage(IReadOnlyLifetime lifetime, IGetRequest request)
        {
            using var downloadHandler = new DownloadHandlerTexture(true);
            using var webRequest = new UnityWebRequest(request.Uri, "GET", downloadHandler, null);
            await webRequest.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);

            if (webRequest.result != UnityWebRequest.Result.Success)
                throw new Exception("GET request failed");

            return downloadHandler.texture;
        }
    }
}