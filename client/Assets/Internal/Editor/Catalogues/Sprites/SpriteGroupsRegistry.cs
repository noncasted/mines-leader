using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Internal {
    public static class SpriteGroupsRegistry {
        public const string AssetPath = "Assets/Internal/Editor/Catalogues/Sprites/SpriteGroups.json";

        private static readonly string[] SourceExtensions = {
            ".aseprite",
            ".psd",
            ".png"
        };

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
                Debug.LogError("[SpriteGroupsRegistry] Group name is empty after sanitize.");
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
                Debug.LogError($"[SpriteGroupsRegistry] Failed to read {AssetPath}: {exception}");
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
                Debug.LogError($"[SpriteGroupsRegistry] Failed to write {AssetPath}: {exception}");
                return false;
            }
        }

        private static IEnumerable<string> DiscoverGroups() {
            var artRoot = Path.Combine(Application.dataPath, "Art");
            if (Directory.Exists(artRoot) == false)
                yield break;

            string[] files;
            try {
                files = Directory.GetFiles(artRoot, "*.*", SearchOption.AllDirectories);
            }
            catch (Exception exception) {
                Debug.LogError($"[SpriteGroupsRegistry] Failed to scan Assets/Art: {exception}");
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var file in files) {
                if (IsSourceFile(file) == false)
                    continue;

                var assetPath = ToAssetPath(projectRoot, file);
                var defaultGroup = SpriteCatalogMetadata.GetDefaultGroup(assetPath);
                if (string.IsNullOrEmpty(defaultGroup) == false && seen.Add(defaultGroup))
                    yield return defaultGroup;

                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer == null)
                    continue;

                if (SpriteCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                if (metadata.Included && string.IsNullOrEmpty(metadata.Group) == false && seen.Add(metadata.Group))
                    yield return metadata.Group;
            }
        }

        private static bool IsSourceFile(string path) {
            var extension = Path.GetExtension(path);
            foreach (var sourceExtension in SourceExtensions) {
                if (extension.Equals(sourceExtension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string ToAssetPath(string projectRoot, string fullPath) {
            var normalized = Path.GetFullPath(fullPath).Replace('\\', '/');
            var root = projectRoot.Replace('\\', '/');
            if (normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase) == false)
                return normalized;

            return normalized.Substring(root.Length).TrimStart('/');
        }

        private static string ToFullPath() {
            return Path.Combine(Application.dataPath, "Internal/Editor/Catalogues/Sprites/SpriteGroups.json");
        }

        [Serializable]
        private sealed class RegistryFile {
            public List<string> groups = new();
        }
    }
}
