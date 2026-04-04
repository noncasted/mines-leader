using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace Internal
{
    public class SceneLoader : ISceneLoader
    {
        public async UniTask<ILoadedScene> Load(AssetReference scene, bool isMain = false)
        {
            var result = await Addressables.LoadSceneAsync(scene, LoadSceneMode.Additive).ToUniTask();

            if (isMain == true)
                SceneManager.SetActiveScene(result.Scene);

            return new LoadedScene(result);
        }
    }
}