using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Internal
{
    public class LoadedScene : ILoadedScene
    {
        public LoadedScene(SceneInstance instance)
        {
            _instance = instance;
        }

        private readonly SceneInstance _instance;

        public SceneInstance Instance => _instance;
        
        public UniTask Unload()
        {
            return Addressables.UnloadSceneAsync(_instance).ToUniTask();
        }
    }
}