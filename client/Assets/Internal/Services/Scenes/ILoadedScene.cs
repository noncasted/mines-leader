using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Internal
{
    public interface ILoadedScene
    {
        SceneInstance Instance { get; }
        
        UniTask Unload();
    }
}