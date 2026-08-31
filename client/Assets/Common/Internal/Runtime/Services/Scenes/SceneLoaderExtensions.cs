using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class SceneLoaderExtensions
    {
        public static async UniTask<(ILoadedScene, T)> LoadTypedResult<T>(
            this ISceneLoader loader,
            AssetReference scene)
        {
            var result = await loader.Load(scene);

            var rootObjects = result.Scene.GetRootGameObjects();

            foreach (var rootObject in rootObjects)
            {
                if (rootObject.TryGetComponent(out T searched) == true)
                    return (result, searched);
            }

            throw new NullReferenceException($"Searched {typeof(T)} is not found");
        }

        public static async UniTask<T> LoadTyped<T>(this ISceneLoader loader, AssetReference scene, bool isMain = false)
        {
            var result = await loader.Load(scene, isMain);

            var rootObjects = result.Scene.GetRootGameObjects();

            foreach (var rootObject in rootObjects)
            {
                if (rootObject.TryGetComponent(out T searched) == true)
                    return searched;
            }

            throw new NullReferenceException($"Searched {typeof(T)} is not found");
        }

        public static async UniTask<T> FindOrLoadScene<T>(
            this IScopeBuilder utils,
            AssetReference scene,
            bool isMain = false)
            where T : MonoBehaviour
        {
#if UNITY_EDITOR
            if (utils.IsMock != true || SceneManager.GetSceneByName(scene.editorAsset.name).IsValid() != true)
                return await utils.SceneLoader.LoadTyped<T>(scene, isMain);

            var targets = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var target in targets)
            {
                if (target.gameObject.scene.name != scene.editorAsset.name)
                    continue;

                return target;
            }

            return Object.FindFirstObjectByType<T>();
#else
            return await utils.SceneLoader.LoadTyped<T>(scene, isMain);
#endif
        }
    }
}