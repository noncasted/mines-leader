using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tools {
    [InitializeOnLoad]
    public static class PrefabCatalogInspector {
        static PrefabCatalogInspector() {
            Editor.finishedDefaultHeaderGUI += Draw;
        }

        private const string GeneratedPrefix = "Assets/Resources/Generated/";
        private const float LabelWidth = 50f;
        private const float AddButtonWidth = 22f;
        private const float OkButtonWidth = 32f;
        private const float CancelButtonWidth = 52f;
        private const string GameObjectOption = "(GameObject)";

        private static bool _isCreatingGroup;
        private static string _newGroupName = string.Empty;

        private static void Draw(Editor editor) {
            if (TryCollectImporters(editor, out var importers) == false)
                return;

            var wasEnabled = GUI.enabled;
            GUI.enabled = true;

            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var states = ResolveStates(importers);
            DrawIncluded(importers, states);

            if (states.Included && states.IncludedMixed == false) {
                DrawGroup(importers, states);
                DrawRoot(importers, states);
            }

            EditorGUILayout.EndVertical();

            EditorGUIUtility.labelWidth = previousLabelWidth;
            GUI.enabled = wasEnabled;
        }

        private static bool TryCollectImporters(Editor editor, out List<AssetImporter> importers) {
            importers = null;
            if (editor == null || editor.targets == null || editor.targets.Length == 0)
                return false;

            var collected = new List<AssetImporter>(editor.targets.Length);
            foreach (var target in editor.targets) {
                if (TryGetImporter(target, out var importer) == false)
                    return false;

                if (IsCatalogTarget(importer) == false)
                    return false;

                collected.Add(importer);
            }

            importers = collected;
            return true;
        }

        private static bool TryGetImporter(Object target, out AssetImporter importer) {
            importer = null;

            // PrefabImporter is internal in this Unity version; AssetImporter covers it.
            if (target is AssetImporter assetImporter) {
                importer = assetImporter;
                return true;
            }

            if (target is not GameObject gameObject)
                return false;

            if (EditorUtility.IsPersistent(gameObject) == false)
                return false;

            if (PrefabUtility.IsPartOfPrefabAsset(gameObject) == false)
                return false;

            if (gameObject.transform.parent != null)
                return false;

            var path = AssetDatabase.GetAssetPath(gameObject);
            if (string.IsNullOrEmpty(path))
                return false;

            importer = AssetImporter.GetAtPath(path);
            return importer != null;
        }

        private static bool IsCatalogTarget(AssetImporter importer) {
            if (string.IsNullOrEmpty(importer.assetPath))
                return false;

            if (importer.assetPath.StartsWith(GeneratedPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            var extension = Path.GetExtension(importer.assetPath);
            return extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static CatalogStates ResolveStates(IReadOnlyList<AssetImporter> importers) {
            var first = PrefabCatalogMetadata.ReadOrDefault(importers[0]);
            var includedMixed = false;
            var groupMixed = false;
            var componentMixed = false;

            for (var i = 1; i < importers.Count; i++) {
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

        private static void DrawIncluded(IReadOnlyList<AssetImporter> importers, CatalogStates states) {
            EditorGUI.showMixedValue = states.IncludedMixed;
            EditorGUI.BeginChangeCheck();
            var included = EditorGUILayout.ToggleLeft("Prefab Catalog", states.Included, EditorStyles.boldLabel);
            var changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            if (changed)
                Apply(importers, metadata => metadata.Included = included);
        }

        private static void DrawGroup(IReadOnlyList<AssetImporter> importers, CatalogStates states) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Group");

            if (_isCreatingGroup) {
                _newGroupName = EditorGUILayout.TextField(_newGroupName);

                if (GUILayout.Button("OK", EditorStyles.miniButton, GUILayout.Width(OkButtonWidth))) {
                    if (PrefabGroupsRegistry.TryAddGroup(_newGroupName, out var groupName)) {
                        Apply(importers, metadata => metadata.Group = groupName);
                        _isCreatingGroup = false;
                        _newGroupName = string.Empty;
                    }
                }

                if (GUILayout.Button("Cancel", EditorStyles.miniButton, GUILayout.Width(CancelButtonWidth))) {
                    _isCreatingGroup = false;
                    _newGroupName = string.Empty;
                }
            }
            else {
                var groups = GetPopupGroups(states.Group);
                var index = IndexOfGroup(groups, states.Group);

                EditorGUI.showMixedValue = states.GroupMixed;
                EditorGUI.BeginChangeCheck();
                var nextIndex = groups.Count == 0
                    ? -1
                    : EditorGUILayout.Popup(Mathf.Max(index, 0), groups.ToArray());
                var groupChanged = EditorGUI.EndChangeCheck();
                EditorGUI.showMixedValue = false;

                if (groupChanged && nextIndex >= 0 && nextIndex < groups.Count)
                    Apply(importers, metadata => metadata.Group = groups[nextIndex]);

                if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(AddButtonWidth))) {
                    _isCreatingGroup = true;
                    _newGroupName = string.Empty;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawRoot(IReadOnlyList<AssetImporter> importers, CatalogStates states) {
            var options = GetRootOptions(importers[0], states.ComponentType);
            var labels = new string[options.Count];
            var index = 0;
            for (var i = 0; i < options.Count; i++) {
                labels[i] = options[i].Display;
                if (string.Equals(options[i].ComponentType, states.ComponentType, StringComparison.Ordinal))
                    index = i;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Root");

            EditorGUI.showMixedValue = states.ComponentMixed;
            EditorGUI.BeginChangeCheck();
            var nextIndex = EditorGUILayout.Popup(index, labels);
            var changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            EditorGUILayout.EndHorizontal();

            if (changed && nextIndex >= 0 && nextIndex < options.Count) {
                var option = options[nextIndex];
                Apply(importers, metadata => {
                    metadata.ComponentType = option.ComponentType;
                    metadata.ComponentGuid = option.ComponentGuid;
                });
            }
        }

        private static void Apply(IReadOnlyList<AssetImporter> importers, Action<PrefabCatalogMetadata> mutate) {
            foreach (var importer in importers) {
                Undo.RecordObject(importer, "Prefab Catalog");
                var metadata = PrefabCatalogMetadata.ReadOrDefault(importer);
                mutate(metadata);
                PrefabCatalogMetadata.Write(importer, metadata);
            }

            PrefabCatalogGenerator.ScheduleGenerate();
        }

        private static List<string> GetPopupGroups(string currentGroup) {
            var groups = new List<string>(PrefabGroupsRegistry.GetGroups());
            if (string.IsNullOrEmpty(currentGroup) == false &&
                IndexOfGroup(groups, currentGroup) < 0)
                groups.Add(currentGroup);

            return groups;
        }

        private static int IndexOfGroup(IReadOnlyList<string> groups, string group) {
            for (var i = 0; i < groups.Count; i++) {
                if (string.Equals(groups[i], group, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static List<RootOption> GetRootOptions(AssetImporter importer, string currentType) {
            var options = new List<RootOption> {
                new RootOption(GameObjectOption, string.Empty, string.Empty)
            };
            var seen = new HashSet<string>(StringComparer.Ordinal) { string.Empty };

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(importer.assetPath);
            if (prefab != null) {
                var behaviours = prefab.GetComponents<MonoBehaviour>();
                foreach (var behaviour in behaviours) {
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

        private readonly struct RootOption {
            public RootOption(string display, string componentType, string componentGuid) {
                Display = display;
                ComponentType = componentType;
                ComponentGuid = componentGuid;
            }

            public string Display { get; }
            public string ComponentType { get; }
            public string ComponentGuid { get; }
        }

        private readonly struct CatalogStates {
            public CatalogStates(
                bool included,
                string group,
                string componentType,
                bool includedMixed,
                bool groupMixed,
                bool componentMixed) {
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
