using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tools {
    [InitializeOnLoad]
    public static class SpriteCatalogInspector {
        static SpriteCatalogInspector() {
            Editor.finishedDefaultHeaderGUI += Draw;
        }

        private const string ArtPrefix = "Assets/Art/";
        private const float LabelWidth = 50f;
        private const float AddButtonWidth = 22f;
        private const float OkButtonWidth = 32f;
        private const float CancelButtonWidth = 52f;

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
                DrawKind(importers, states);
                DrawGroup(importers, states);

                if (states.KindMixed == false && states.Kind == SpriteCatalogKind.Animation) {
                    DrawTime(importers, states);
                    DrawColor(importers, states);
                }
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
                if (target is not AssetImporter importer)
                    return false;

                if (IsCatalogTarget(importer) == false)
                    return false;

                collected.Add(importer);
            }

            importers = collected;
            return true;
        }

        private static bool IsCatalogTarget(AssetImporter importer) {
            if (string.IsNullOrEmpty(importer.assetPath))
                return false;

            if (importer.assetPath.StartsWith(ArtPrefix, StringComparison.OrdinalIgnoreCase) == false)
                return false;

            var extension = Path.GetExtension(importer.assetPath);
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".psd", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".aseprite", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".ase", StringComparison.OrdinalIgnoreCase);
        }

        private static CatalogStates ResolveStates(IReadOnlyList<AssetImporter> importers) {
            var first = SpriteCatalogMetadata.ReadOrDefault(importers[0]);
            var includedMixed = false;
            var kindMixed = false;
            var groupMixed = false;
            var timeMixed = false;
            var colorMixed = false;

            for (var i = 1; i < importers.Count; i++) {
                var metadata = SpriteCatalogMetadata.ReadOrDefault(importers[i]);
                if (metadata.Included != first.Included)
                    includedMixed = true;
                if (metadata.Kind != first.Kind)
                    kindMixed = true;
                if (string.Equals(metadata.Group, first.Group, StringComparison.Ordinal) == false)
                    groupMixed = true;
                if (Mathf.Approximately(metadata.Time, first.Time) == false)
                    timeMixed = true;
                if (metadata.Color != first.Color)
                    colorMixed = true;
            }

            return new CatalogStates(
                first.Included,
                first.Kind,
                first.Group,
                first.Time,
                first.Color,
                includedMixed,
                kindMixed,
                groupMixed,
                timeMixed,
                colorMixed);
        }

        private static void DrawIncluded(IReadOnlyList<AssetImporter> importers, CatalogStates states) {
            EditorGUI.showMixedValue = states.IncludedMixed;
            EditorGUI.BeginChangeCheck();
            var included = EditorGUILayout.ToggleLeft("Sprite Catalog", states.Included, EditorStyles.boldLabel);
            var changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            if (changed)
                Apply(importers, metadata => metadata.Included = included);
        }

        private static void DrawKind(IReadOnlyList<AssetImporter> importers, CatalogStates states) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Kind");

            var isSheet = states.KindMixed == false && states.Kind == SpriteCatalogKind.Sheet;
            var isAnimation = states.KindMixed == false && states.Kind == SpriteCatalogKind.Animation;

            var nextSheet = GUILayout.Toggle(isSheet, "Sheet", EditorStyles.radioButton);
            var nextAnimation = GUILayout.Toggle(isAnimation, "Animation", EditorStyles.radioButton);

            EditorGUILayout.EndHorizontal();

            if (nextSheet && isSheet == false)
                Apply(importers, metadata => metadata.Kind = SpriteCatalogKind.Sheet);
            else if (nextAnimation && isAnimation == false)
                Apply(importers, SwitchToAnimation);
        }

        private static void DrawGroup(IReadOnlyList<AssetImporter> importers, CatalogStates states) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Group");

            if (_isCreatingGroup) {
                _newGroupName = EditorGUILayout.TextField(_newGroupName);

                if (GUILayout.Button("OK", EditorStyles.miniButton, GUILayout.Width(OkButtonWidth))) {
                    if (SpriteGroupsRegistry.TryAddGroup(_newGroupName, out var groupName)) {
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

        private static void DrawTime(IReadOnlyList<AssetImporter> importers, CatalogStates states) {
            EditorGUI.showMixedValue = states.TimeMixed;
            EditorGUI.BeginChangeCheck();
            var time = EditorGUILayout.FloatField("Time", states.Time);
            var changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            if (changed)
                Apply(importers, metadata => metadata.Time = time);
        }

        private static void DrawColor(IReadOnlyList<AssetImporter> importers, CatalogStates states) {
            EditorGUI.showMixedValue = states.ColorMixed;
            EditorGUI.BeginChangeCheck();
            var color = EditorGUILayout.ColorField("Color", states.Color);
            var changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            if (changed)
                Apply(importers, metadata => metadata.Color = color);
        }

        private static void Apply(IReadOnlyList<AssetImporter> importers, Action<SpriteCatalogMetadata> mutate) {
            foreach (var importer in importers) {
                Undo.RecordObject(importer, "Sprite Catalog");
                var metadata = SpriteCatalogMetadata.ReadOrDefault(importer);
                mutate(metadata);
                if (metadata.Included && string.IsNullOrEmpty(metadata.Group))
                    metadata.Group = SpriteCatalogMetadata.GetDefaultGroup(importer.assetPath);

                SpriteCatalogMetadata.Write(importer, metadata);
            }

            SpriteGenerator.ScheduleGenerate();
        }

        private static void SwitchToAnimation(SpriteCatalogMetadata metadata) {
            if (metadata.Kind != SpriteCatalogKind.Animation) {
                metadata.Time = SpriteCatalogMetadata.DefaultTime;
                metadata.Color = SpriteCatalogMetadata.DefaultColor;
            }

            metadata.Kind = SpriteCatalogKind.Animation;
        }

        private static List<string> GetPopupGroups(string currentGroup) {
            var groups = new List<string>(SpriteGroupsRegistry.GetGroups());
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

        private readonly struct CatalogStates {
            public CatalogStates(
                bool included,
                SpriteCatalogKind kind,
                string group,
                float time,
                Color color,
                bool includedMixed,
                bool kindMixed,
                bool groupMixed,
                bool timeMixed,
                bool colorMixed) {
                Included = included;
                Kind = kind;
                Group = group;
                Time = time;
                Color = color;
                IncludedMixed = includedMixed;
                KindMixed = kindMixed;
                GroupMixed = groupMixed;
                TimeMixed = timeMixed;
                ColorMixed = colorMixed;
            }

            public bool Included { get; }
            public SpriteCatalogKind Kind { get; }
            public string Group { get; }
            public float Time { get; }
            public Color Color { get; }
            public bool IncludedMixed { get; }
            public bool KindMixed { get; }
            public bool GroupMixed { get; }
            public bool TimeMixed { get; }
            public bool ColorMixed { get; }
        }
    }
}
