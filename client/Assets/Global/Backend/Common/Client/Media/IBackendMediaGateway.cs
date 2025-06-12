using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Global.Backend
{
    public interface IBackendMediaGateway
    {
        UniTask<AudioClip> GetAudio(IReadOnlyLifetime lifetime, IGetRequest request, AudioType audioType);
        UniTask<Texture2D> GetImage(IReadOnlyLifetime lifetime, IGetRequest request);
    }
}