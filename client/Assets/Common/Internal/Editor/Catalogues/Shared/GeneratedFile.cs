using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal {
    // Запись сгенерированных скриптов: пишем только при изменении содержимого и подчищаем то,
    // что генератор в этот заход не выдал.
    public static class GeneratedFile {
        public static void WriteIfChanged(string logTag, string assetPath, string content) {
            try {
                var fullPath = CatalogPaths.ToFullPath(assetPath);
                if (File.Exists(fullPath) && File.ReadAllText(fullPath) == content)
                    return;

                var directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
                    Directory.CreateDirectory(directory);

                File.WriteAllText(fullPath, content);
                AssetDatabase.ImportAsset(assetPath);
                Debug.Log($"[{logTag}] Generated {assetPath}");
            }
            catch (Exception exception) {
                Debug.LogError($"[{logTag}] Failed to write {assetPath}: {exception}");
            }
        }

        // Папку Generated делят несколько генераторов, поэтому чужие файлы отсекаются по суффиксу
        // имени класса: каталог префабов трогает только *Prefabs.cs, каталог ассетов — *Assets.cs.
        public static void DeleteStale(string logTag, string folder, HashSet<string> writtenNames, string ownedSuffix) {
            foreach (var file in EnumerateScripts(logTag, folder)) {
                var fileName = Path.GetFileName(file);
                if (writtenNames.Contains(fileName) || IsOwned(fileName, ownedSuffix) == false)
                    continue;

                Delete(logTag, $"{folder}/{fileName}", file);
            }
        }

        public static void DeleteFolderScripts(string logTag, string folder, string ownedSuffix) {
            foreach (var file in EnumerateScripts(logTag, folder)) {
                var fileName = Path.GetFileName(file);
                if (IsOwned(fileName, ownedSuffix) == false)
                    continue;

                Delete(logTag, $"{folder}/{fileName}", file);
            }
        }

        private static bool IsOwned(string fileName, string ownedSuffix) {
            return fileName.EndsWith(ownedSuffix, StringComparison.Ordinal);
        }

        private static IReadOnlyList<string> EnumerateScripts(string logTag, string folder) {
            if (AssetDatabase.IsValidFolder(folder) == false)
                return Array.Empty<string>();

            try {
                return Directory.GetFiles(CatalogPaths.ToFullPath(folder), "*.cs", SearchOption.TopDirectoryOnly);
            }
            catch (Exception exception) {
                Debug.LogError($"[{logTag}] Failed to scan {folder}: {exception}");
                return Array.Empty<string>();
            }
        }

        private static void Delete(string logTag, string assetPath, string fullPath) {
            if (AssetDatabase.DeleteAsset(assetPath) == false) {
                try {
                    File.Delete(fullPath);
                }
                catch (Exception exception) {
                    Debug.LogError($"[{logTag}] Failed to delete {assetPath}: {exception}");
                    return;
                }
            }

            Debug.Log($"[{logTag}] Deleted stale file: {assetPath}");
        }
    }
}
