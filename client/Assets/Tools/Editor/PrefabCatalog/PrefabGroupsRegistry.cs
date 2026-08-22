using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Tools {
    public static class PrefabGroupsRegistry {
        public const string AssetPath = "Assets/Tools/Editor/PrefabCatalog/PrefabGroups.json";

        private static readonly JsonSerializerSettings JsonSettings = new() {
            Formatting = Formatting.Indented
        };

        private static string[] _cachedGroups;

        public static IReadOnlyList<string> GetGroups() {
            if (_cachedGroups != null)
                return _cachedGroups;

            var names = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var group in ReadFileGroups())
                names.Add(group);

            foreach (var group in DiscoverGroups())
                names.Add(group);

            _cachedGroups = new string[names.Count];
            names.CopyTo(_cachedGroups);
            return _cachedGroups;
        }

        public static bool TryAddGroup(string name, out string groupName) {
            groupName = SpriteCatalogMetadata.ToGroupName(name);
            if (string.IsNullOrEmpty(groupName)) {
                Debug.LogError("[PrefabGroupsRegistry] Group name is empty after sanitize.");
                return false;
            }

            var groups = new List<string>(ReadFileGroups());
            foreach (var existing in groups) {
                if (string.Equals(existing, groupName, StringComparison.OrdinalIgnoreCase)) {
                    groupName = existing;
                    Invalidate();
                    return true;
                }
            }

            groups.Add(groupName);
            if (WriteFileGroups(groups) == false)
                return false;

            Invalidate();
            return true;
        }

        public static void Invalidate() {
            _cachedGroups = null;
        }

        private static IReadOnlyList<string> ReadFileGroups() {
            var fullPath = ToFullPath();

            try {
                if (File.Exists(fullPath) == false)
                    return Array.Empty<string>();

                var json = File.ReadAllText(fullPath);
                var file = JsonConvert.DeserializeObject<RegistryFile>(json);
                if (file?.groups == null)
                    return Array.Empty<string>();

                var groups = new List<string>(file.groups.Count);
                foreach (var group in file.groups) {
                    if (string.IsNullOrWhiteSpace(group) == false)
                        groups.Add(group);
                }

                return groups;
            }
            catch (Exception exception) {
                Debug.LogError($"[PrefabGroupsRegistry] Failed to read {AssetPath}: {exception}");
                return Array.Empty<string>();
            }
        }

        private static bool WriteFileGroups(IReadOnlyList<string> groups) {
            var fullPath = ToFullPath();

            try {
                var directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
                    Directory.CreateDirectory(directory);

                var file = new RegistryFile { groups = new List<string>(groups) };
                var json = JsonConvert.SerializeObject(file, JsonSettings);
                File.WriteAllText(fullPath, json);
                AssetDatabase.ImportAsset(AssetPath);
                return true;
            }
            catch (Exception exception) {
                Debug.LogError($"[PrefabGroupsRegistry] Failed to write {AssetPath}: {exception}");
                return false;
            }
        }

        private static IEnumerable<string> DiscoverGroups() {
            var assetsRoot = Application.dataPath;
            if (Directory.Exists(assetsRoot) == false)
                yield break;

            string[] files;
            try {
                files = Directory.GetFiles(assetsRoot, "*.prefab", SearchOption.AllDirectories);
            }
            catch (Exception exception) {
                Debug.LogError($"[PrefabGroupsRegistry] Failed to scan Assets: {exception}");
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var file in files) {
                var assetPath = ToAssetPath(projectRoot, file);
                if (PrefabCatalogGenerator.IsGeneratedPath(assetPath))
                    continue;

                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer == null)
                    continue;

                if (PrefabCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                if (metadata.Included && string.IsNullOrEmpty(metadata.Group) == false && seen.Add(metadata.Group))
                    yield return metadata.Group;
            }
        }

        private static string ToAssetPath(string projectRoot, string fullPath) {
            var normalized = Path.GetFullPath(fullPath).Replace('\\', '/');
            var root = projectRoot.Replace('\\', '/');
            if (normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase) == false)
                return normalized;

            return normalized.Substring(root.Length).TrimStart('/');
        }

        private static string ToFullPath() {
            return Path.Combine(Application.dataPath, "Tools/Editor/PrefabCatalog/PrefabGroups.json");
        }

        [Serializable]
        private sealed class RegistryFile {
            public List<string> groups = new();
        }
    }
}
