using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [InlineEditor]
    public class AssetsStorage : ScriptableObject, IAssetsStorage
    {
        [SerializeField] private List<EnvAsset> _assets;
        [SerializeField] private OptionsDictionary _options;

        private readonly Dictionary<string, IReadOnlyList<EnvAsset>> _convertedAssets = new();

        public IReadOnlyDictionary<string, IReadOnlyList<EnvAsset>> Assets => _convertedAssets;
        public IReadOnlyDictionary<PlatformType, OptionsRegistry> Options => _options;

        public void Cache()
        {
            _convertedAssets.Clear();
            
            foreach (var asset in _assets)
            {
                var key = GetKey();

                if (_convertedAssets.TryGetValue(key, out var list) == false)
                {
                    list = new List<EnvAsset>();
                    _convertedAssets.Add(key, list);
                }

                ((List<EnvAsset>)list).Add(asset);

                string GetKey()
                {
                    if (asset is IEnvAssetKeyOverride keyOverride)
                        return keyOverride.GetKeyOverride();

                    return asset.GetType().FullName!;
                }
            }
            
        }

        [Button]
        public void Scan()
        {
#if UNITY_EDITOR
            _assets.Clear();

            AssetDatabase.Refresh();
            
            var all = GetAssets();
            var index = GetMaxIndex();
            var ids = new HashSet<int>();

            foreach (var asset in all)
            {
                if (asset is IEnvAssetValidator validator)
                    validator.OnValidation();

                if (asset.Id == -1 || ids.Contains(asset.Id))
                {
                    asset.SetId(index);
                    index++;
                    EditorUtility.SetDirty(asset);
                }

                ids.Add(asset.Id);
                _assets.Add(asset);
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            IReadOnlyList<EnvAsset> GetAssets()
            {
                var items = AssetDatabase.FindAssets("t:EnvAsset", new[] { "Assets/" }).ToArray();
                var assets = new List<EnvAsset>();

                foreach (var guid in items)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<EnvAsset>(path);
                    assets.Add(asset);
                }

                return assets;
            }

            int GetMaxIndex()
            {
                var maxIndex = -1;

                foreach (var asset in all)
                {
                    if (asset.Id > maxIndex)
                        maxIndex = asset.Id;
                }

                return maxIndex;
            }
#endif
        }

#if UNITY_EDITOR
        [InitializeOnLoad]
        public static class StorageScanner
        {
            private static bool _isScanning;

            static StorageScanner()
            {
                ScanAssets();
            }

            [MenuItem("Assets/Scan assets %w", priority = -1000)]
            public static void ScanAssets()
            {
                if (_isScanning == true)
                    return;

                var ids = AssetDatabase.FindAssets("t:AssetsStorage");

                if (ids.Length == 0 || ids.Length > 1)
                    throw new Exception();

                _isScanning = true;

                var path = AssetDatabase.GUIDToAssetPath(ids[0]);
                var storage = AssetDatabase.LoadAssetAtPath<AssetsStorage>(path);

                try
                {
                    storage.Scan();
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }

                _isScanning = false;
                EditorUtility.SetDirty(storage);
            }
        }
#endif
    }
}