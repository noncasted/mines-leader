using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Internal
{
    [InitializeOnLoad]
    public static class AssetCatalogInspector
    {
        static AssetCatalogInspector()
        {
            Editor.finishedDefaultHeaderGUI += Draw;
        }

        private static void Draw(Editor editor)
        {
            if (CatalogInspectorGUI.TryCollectImporters(editor, IsCatalogTarget, out var importers) == false)
                return;

            using (CatalogInspectorGUI.BeginSection())
            {
                var states = ResolveStates(importers);
                DrawIncluded(importers, states);

                if (states.Included == false || states.IncludedMixed)
                    return;

                CatalogGroupField.Draw(
                    AssetGroupsRegistry.Instance,
                    states.Group,
                    states.GroupMixed,
                    group => Apply(importers, metadata => metadata.Group = group));
            }
        }

        // Каталог собирает только EnvAsset, поэтому обычные ScriptableObject-ассеты блок не показывают.
        private static bool IsCatalogTarget(AssetImporter importer)
        {
            if (string.IsNullOrEmpty(importer.assetPath))
                return false;

            if (Path.GetExtension(importer.assetPath).Equals(".asset", StringComparison.OrdinalIgnoreCase) == false)
                return false;

            return AssetDatabase.LoadAssetAtPath<EnvAsset>(importer.assetPath) != null;
        }

        private static CatalogStates ResolveStates(IReadOnlyList<AssetImporter> importers)
        {
            var first = AssetCatalogMetadata.ReadOrDefault(importers[0]);
            var includedMixed = false;
            var groupMixed = false;

            for (var i = 1; i < importers.Count; i++)
            {
                var metadata = AssetCatalogMetadata.ReadOrDefault(importers[i]);

                if (metadata.Included != first.Included)
                    includedMixed = true;

                if (string.Equals(metadata.Group, first.Group, StringComparison.Ordinal) == false)
                    groupMixed = true;
            }

            return new CatalogStates(first.Included, first.Group, includedMixed, groupMixed);
        }

        private static void DrawIncluded(IReadOnlyList<AssetImporter> importers, CatalogStates states)
        {
            if (CatalogInspectorGUI.TryDrawToggle("Asset Catalog", states.Included, states.IncludedMixed,
                out var included))
                Apply(importers, metadata => metadata.Included = included);
        }

        private static void Apply(IReadOnlyList<AssetImporter> importers, Action<AssetCatalogMetadata> mutate)
        {
            foreach (var importer in importers)
            {
                Undo.RecordObject(importer, "Asset Catalog");
                var metadata = AssetCatalogMetadata.ReadOrDefault(importer);
                mutate(metadata);
                AssetCatalogMetadata.Write(importer, metadata);
            }

            AssetCatalogGenerator.ScheduleGenerate();
        }

        private readonly struct CatalogStates
        {
            public CatalogStates(bool included, string group, bool includedMixed, bool groupMixed)
            {
                Included = included;
                Group = group;
                IncludedMixed = includedMixed;
                GroupMixed = groupMixed;
            }

            public bool Included { get; }
            public string Group { get; }
            public bool IncludedMixed { get; }
            public bool GroupMixed { get; }
        }
    }
}