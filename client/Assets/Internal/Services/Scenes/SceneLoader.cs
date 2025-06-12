using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace Internal
{
    public class SceneLoader : ISceneLoader
    {
        public async UniTask<ILoadedScene> Load(SceneData sceneAsset, bool isMain = false)
        {
            var scene = await Addressables.LoadSceneAsync(sceneAsset.Value, LoadSceneMode.Additive).ToUniTask();

            if (isMain == true)
                SceneManager.SetActiveScene(scene.Scene);

            return new LoadedScene(scene);
        }
    }
}