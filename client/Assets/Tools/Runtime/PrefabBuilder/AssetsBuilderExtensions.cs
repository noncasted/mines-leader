using UnityEngine;
using Object = UnityEngine.Object;

namespace Tools.PrefabBuilder
{
    public static class AssetsBuilderExtensions
    {
        public static PrefabBuilder FromPrefab(string assetPath)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefab == null)
            {
                Debug.LogError($"[PrefabBuilder] Base prefab not found at '{assetPath}'");
                return null;
            }

            var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);

            if (instance == null)
            {
                Debug.LogError($"[PrefabBuilder] Failed to instantiate prefab at '{assetPath}'");
                return null;
            }

            var builder = PrefabBuilder.FromGameObject(instance);
            builder.WithName(instance.name);
            return builder;
#endif
            return null;
        }

        public static T LoadAsset<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                Debug.LogWarning($"[PrefabBuilder] Asset not found at '{path}'");
            }

            return asset;
#endif
            return null;
        }

        public static T LoadSubAsset<T>(string path, string subAssetName) where T : Object
        {
#if UNITY_EDITOR
            var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (var asset in allAssets)
            {
                if (asset is T typed && asset.name == subAssetName)
                    return typed;
            }
#endif

            Debug.LogWarning($"[PrefabBuilder] Sub-asset '{subAssetName}' not found at '{path}'");
            return null;
        }
    }
}
