using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class SceneLoaderExtensions
    {
        public static async UniTask<(ILoadedScene, T)> LoadTypedResult<T>(this ISceneLoader loader, SceneData data)
        {
            var result = await loader.Load(data);

            var rootObjects = result.Instance.Scene.GetRootGameObjects();

            foreach (var rootObject in rootObjects)
            {
                if (rootObject.TryGetComponent(out T searched) == true)
                    return (result, searched);
            }

            throw new NullReferenceException($"Searched {typeof(T)} is not found");
        }

        public static async UniTask<T> LoadTyped<T>(this ISceneLoader loader, SceneData data, bool isMain = false)
        {
            var result = await loader.Load(data, isMain);

            var rootObjects = result.Instance.Scene.GetRootGameObjects();

            foreach (var rootObject in rootObjects)
            {
                if (rootObject.TryGetComponent(out T searched) == true)
                    return searched;
            }

            throw new NullReferenceException($"Searched {typeof(T)} is not found");
        }

        public static async UniTask<T> FindOrLoadScene<T>(this IScopeBuilder utils, SceneData data, bool isMain = false)
            where T : MonoBehaviour
        {
#if UNITY_EDITOR
            if (utils.IsMock != true || SceneManager.GetSceneByName(data.Value.editorAsset.name).IsValid() != true)
                return await utils.SceneLoader.LoadTyped<T>(data, isMain);

            var targets = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var target in targets)
            {
                if (target.gameObject.scene.name != data.Value.editorAsset.name)
                    continue;

                return target;
            }

            return Object.FindFirstObjectByType<T>();
#else
            return await utils.SceneLoader.LoadTyped<T>(data, isMain);
#endif
        }
    }
}