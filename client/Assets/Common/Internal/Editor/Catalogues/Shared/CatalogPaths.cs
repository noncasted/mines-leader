using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal {
    public static class CatalogPaths {
        public static string ToFullPath(string assetPath) {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, assetPath);
        }

        public static string ToAssetPath(string fullPath) {
            return ToAssetPath(Path.GetDirectoryName(Application.dataPath), fullPath);
        }

        public static string ToAssetPath(string projectRoot, string fullPath) {
            var normalized = Path.GetFullPath(fullPath).Replace('\\', '/');
            var root = projectRoot.Replace('\\', '/');
            if (normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase) == false)
                return normalized;

            return normalized.Substring(root.Length).TrimStart('/');
        }

        public static void EnsureFolder(string folderPath) {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++) {
                var next = current + "/" + parts[i];
                if (AssetDatabase.IsValidFolder(next) == false)
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
