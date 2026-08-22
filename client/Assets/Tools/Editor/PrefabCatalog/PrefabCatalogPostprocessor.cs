using System;
using System.IO;
using UnityEditor;

namespace Tools {
    public sealed class PrefabCatalogPostprocessor : AssetPostprocessor {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths) {
            if (ContainsPrefab(deletedAssets) ||
                ContainsPrefab(movedAssets) ||
                ContainsPrefab(movedFromAssetPaths) ||
                ContainsIncludedPrefab(importedAssets))
                PrefabCatalogGenerator.ScheduleGenerate();
        }

        private static bool ContainsPrefab(string[] paths) {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++) {
                if (IsPrefabPath(paths[i]))
                    return true;
            }

            return false;
        }

        private static bool ContainsIncludedPrefab(string[] paths) {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++) {
                var path = paths[i];
                if (IsPrefabPath(path) == false)
                    continue;

                var importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                    continue;

                if (PrefabCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                if (metadata.Included)
                    return true;
            }

            return false;
        }

        private static bool IsPrefabPath(string path) {
            if (string.IsNullOrEmpty(path))
                return false;

            if (PrefabCatalogGenerator.IsGeneratedPath(path))
                return false;

            return Path.GetExtension(path).Equals(".prefab", StringComparison.OrdinalIgnoreCase);
        }
    }
}
