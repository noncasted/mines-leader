using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [InitializeOnLoad]
    public static class PrefabCatalogInspector
    {
        static PrefabCatalogInspector()
        {
            Editor.finishedDefaultHeaderGUI += Draw;
        }

        private const string GameObjectOption = "(GameObject)";

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
                    PrefabGroupsRegistry.Instance,
                    states.Group,
                    states.GroupMixed,
                    group => Apply(importers, metadata => metadata.Group = group));

                DrawRoot(importers, states);
            }
        }

        private static bool IsCatalogTarget(AssetImporter importer)
        {
            if (string.IsNullOrEmpty(importer.assetPath))
                return false;

            return Path.GetExtension(importer.assetPath).Equals(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static CatalogStates ResolveStates(IReadOnlyList<AssetImporter> importers)
        {
            var first = PrefabCatalogMetadata.ReadOrDefault(importers[0]);
            var includedMixed = false;
            var groupMixed = false;
            var componentMixed = false;

            for (var i = 1; i < importers.Count; i++)
            {
                var metadata = PrefabCatalogMetadata.ReadOrDefault(importers[i]);

                if (metadata.Included != first.Included)
                    includedMixed = true;

                if (string.Equals(metadata.Group, first.Group, StringComparison.Ordinal) == false)
                    groupMixed = true;

                if (string.Equals(metadata.ComponentType, first.ComponentType, StringComparison.Ordinal) == false)
                    componentMixed = true;
            }

            return new CatalogStates(
                first.Included,
                first.Group,
                first.ComponentType,
                includedMixed,
                groupMixed,
                componentMixed);
        }

        private static void DrawIncluded(IReadOnlyList<AssetImporter> importers, CatalogStates states)
        {
            if (CatalogInspectorGUI.TryDrawToggle("Prefab Catalog", states.Included, states.IncludedMixed,
                out var included))
                Apply(importers, metadata => metadata.Included = included);
        }

        private static void DrawRoot(IReadOnlyList<AssetImporter> importers, CatalogStates states)
        {
            var options = GetRootOptions(importers[0], states.ComponentType);
            var labels = new List<string>(options.Count);
            var index = 0;

            for (var i = 0; i < options.Count; i++)
            {
                labels.Add(options[i].Display);

                if (string.Equals(options[i].ComponentType, states.ComponentType, StringComparison.Ordinal))
                    index = i;
            }

            if (CatalogInspectorGUI.TryDrawPopup("Root", labels, index, states.ComponentMixed, out var nextIndex) ==
                false)
                return;

            var option = options[nextIndex];

            Apply(importers, metadata => {
                metadata.ComponentType = option.ComponentType;
                metadata.ComponentGuid = option.ComponentGuid;
            });
        }

        private static void Apply(IReadOnlyList<AssetImporter> importers, Action<PrefabCatalogMetadata> mutate)
        {
            foreach (var importer in importers)
            {
                Undo.RecordObject(importer, "Prefab Catalog");
                var metadata = PrefabCatalogMetadata.ReadOrDefault(importer);
                mutate(metadata);
                PrefabCatalogMetadata.Write(importer, metadata);
            }

            PrefabCatalogGenerator.ScheduleGenerate();
        }

        private static List<RootOption> GetRootOptions(AssetImporter importer, string currentType)
        {
            var options = new List<RootOption>
            {
                new RootOption(GameObjectOption, string.Empty, string.Empty)
            };
            var seen = new HashSet<string>(StringComparer.Ordinal) { string.Empty };

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(importer.assetPath);

            if (prefab != null)
            {
                var behaviours = prefab.GetComponents<MonoBehaviour>();

                foreach (var behaviour in behaviours)
                {
                    if (behaviour == null)
                        continue;

                    var type = behaviour.GetType();
                    var qualified = type.AssemblyQualifiedName ?? string.Empty;

                    if (string.IsNullOrEmpty(qualified) || seen.Add(qualified) == false)
                        continue;

                    options.Add(new RootOption(type.Name, qualified, PrefabCatalogMetadata.ToScriptGuid(behaviour)));
                }
            }

            if (string.IsNullOrEmpty(currentType) == false && seen.Add(currentType))
                options.Add(new RootOption(
                    PrefabCatalogMetadata.ToDisplayName(currentType),
                    currentType,
                    string.Empty));

            return options;
        }

        private readonly struct RootOption
        {
            public RootOption(string display, string componentType, string componentGuid)
            {
                Display = display;
                ComponentType = componentType;
                ComponentGuid = componentGuid;
            }

            public string Display { get; }
            public string ComponentType { get; }
            public string ComponentGuid { get; }
        }

        private readonly struct CatalogStates
        {
            public CatalogStates(
                bool included,
                string group,
                string componentType,
                bool includedMixed,
                bool groupMixed,
                bool componentMixed)
            {
                Included = included;
                Group = group;
                ComponentType = componentType;
                IncludedMixed = includedMixed;
                GroupMixed = groupMixed;
                ComponentMixed = componentMixed;
            }

            public bool Included { get; }
            public string Group { get; }
            public string ComponentType { get; }
            public bool IncludedMixed { get; }
            public bool GroupMixed { get; }
            public bool ComponentMixed { get; }
        }
    }
}