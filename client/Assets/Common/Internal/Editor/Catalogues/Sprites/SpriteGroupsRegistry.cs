using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal {
    [NoAutoStaticsCleanup]
    public sealed class SpriteGroupsRegistry : CatalogGroupsRegistry {
        private static readonly string[] SourceExtensions = {
            ".aseprite",
            ".psd",
            ".png"
        };

        private SpriteGroupsRegistry()
            : base("SpriteGroupsRegistry", "Assets/Common/Internal/Editor/Catalogues/Sprites/SpriteGroups.json") {
        }

        public static SpriteGroupsRegistry Instance { get; } = new();

        protected override IEnumerable<string> DiscoverGroups() {
            var artRoot = Path.Combine(Application.dataPath, "Art");
            if (Directory.Exists(artRoot) == false)
                yield break;

            string[] files;
            try {
                files = Directory.GetFiles(artRoot, "*.*", SearchOption.AllDirectories);
            }
            catch (Exception exception) {
                Debug.LogError($"[{LogTag}] Failed to scan Assets/Art: {exception}");
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var file in files) {
                if (IsSourceFile(file) == false)
                    continue;

                var assetPath = CatalogPaths.ToAssetPath(projectRoot, file);
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
    }
}
