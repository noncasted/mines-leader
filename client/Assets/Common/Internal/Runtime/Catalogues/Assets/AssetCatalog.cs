using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal {
    // Каталог грузится один раз на старте: он целиком лежит в Resources, отдельных
    // единиц загрузки у групп нет — группа задаёт только пространство имён.
    [AutoStaticsCleanup]
    public static partial class AssetCatalog {
        public const string ResourcesPath = "AssetCatalog";

        private static readonly Dictionary<string, EnvAsset> Entries = new(StringComparer.Ordinal);

        public static bool IsLoaded { get; private set; }

        public static void Load() {
            if (IsLoaded)
                return;

            var catalog = Resources.Load<EnvAssetCatalogAsset>(ResourcesPath);
            if (catalog == null)
                throw new InvalidOperationException(
                    $"Asset catalog is missing at Resources/{ResourcesPath}. Run Tools/GenerateAssetsCatalog.");

            Entries.Clear();
            foreach (var entry in catalog.Entries) {
                if (entry == null || entry.Asset == null) {
                    Debug.LogError($"[AssetCatalog] Empty entry '{entry?.Group}.{entry?.Name}' is skipped.");
                    continue;
                }

                Entries[Key(entry.Group, entry.Name)] = entry.Asset;
            }

            IsLoaded = true;
        }

        public static T Get<T>(string group, string name) where T : EnvAsset {
            if (IsLoaded == false)
                throw new InvalidOperationException($"Asset catalog is not loaded, cannot read {group}.{name}");

            if (Entries.TryGetValue(Key(group, name), out var asset) == false)
                throw new InvalidOperationException($"Asset '{group}.{name}' is missing in the catalog");

            if (asset is not T typed)
                throw new InvalidOperationException(
                    $"Asset '{group}.{name}' is {asset.GetType().Name}, expected {typeof(T).Name}");

            return typed;
        }

        private static string Key(string group, string name) {
            return group + "." + name;
        }
    }
}
