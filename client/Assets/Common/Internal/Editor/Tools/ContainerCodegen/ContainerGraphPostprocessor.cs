using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal {
    public sealed class ContainerGraphPostprocessor : AssetPostprocessor {
        private static readonly CatalogGenerationRunner Runner = new("ContainerGraph", GenerateInternal);

        [InitializeOnLoadMethod]
        private static void OnEditorReload() {
            Runner.RunDelayed();
        }

        [MenuItem("Tools/GenerateContainerGraphAssets")]
        public static void Generate() {
            Runner.Run();
        }

        public static void ScheduleGenerate() {
            Runner.Schedule();
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths) {
            if (ContainsTarget(importedAssets) ||
                ContainsTarget(deletedAssets) ||
                ContainsTarget(movedAssets) ||
                ContainsTarget(movedFromAssetPaths))
                ScheduleGenerate();
        }

        private static void GenerateInternal() {
            var assetsRoot = Application.dataPath;
            var assets = ContainerGraphAssetScanner.Scan(assetsRoot);
            ContainerGraphAssetWriter.Write(assets);
            Debug.Log("[ContainerGraph] Wrote " + assets.Count + " asset graph entries.");
        }

        private static bool ContainsTarget(string[] paths) {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++) {
                if (IsTarget(paths[i]))
                    return true;
            }

            return false;
        }

        private static bool IsTarget(string path) {
            if (string.IsNullOrEmpty(path))
                return false;

            var extension = Path.GetExtension(path);
            return extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".unity", StringComparison.OrdinalIgnoreCase);
        }
    }
}
