#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tools.PrefabBuilder
{
    public static class AssetsBuilderExtensions
    {
        public static PrefabBuilder FromPrefab(string assetPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefab == null)
            {
                Debug.LogError($"[PrefabBuilder] Base prefab not found at '{assetPath}'");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            if (instance == null)
            {
                Debug.LogError($"[PrefabBuilder] Failed to instantiate prefab at '{assetPath}'");
                return null;
            }

            var builder = PrefabBuilder.FromGameObject(instance);
            builder.WithName(instance.name);
            return builder;
        }

        public static T LoadAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                Debug.LogWarning($"[PrefabBuilder] Asset not found at '{path}'");
            }

            return asset;
        }

        public static T LoadSubAsset<T>(string path, string subAssetName) where T : Object
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (var asset in allAssets)
            {
                if (asset is T typed && asset.name == subAssetName)
                    return typed;
            }

            Debug.LogWarning($"[PrefabBuilder] Sub-asset '{subAssetName}' not found at '{path}'");
            return null;
        }
    }
}
#endif