using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public sealed class AssetGroupsRegistry : CatalogGroupsRegistry
    {
        private AssetGroupsRegistry()
            : base("AssetGroupsRegistry", "Assets/Common/Internal/Editor/Catalogues/Assets/AssetGroups.json")
        {
        }

        public static AssetGroupsRegistry Instance { get; } = new();

        protected override IEnumerable<string> DiscoverGroups()
        {
            var assetsRoot = Application.dataPath;

            if (Directory.Exists(assetsRoot) == false)
                yield break;

            string[] files;

            try
            {
                files = Directory.GetFiles(assetsRoot, "*.asset", SearchOption.AllDirectories);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{LogTag}] Failed to scan Assets: {exception}");
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var file in files)
            {
                var assetPath = CatalogPaths.ToAssetPath(projectRoot, file);
                var importer = AssetImporter.GetAtPath(assetPath);

                if (importer == null)
                    continue;

                if (AssetCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                if (metadata.Included && string.IsNullOrEmpty(metadata.Group) == false && seen.Add(metadata.Group))
                    yield return metadata.Group;
            }
        }
    }
}