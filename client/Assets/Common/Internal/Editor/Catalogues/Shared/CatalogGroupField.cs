using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal {
    // Выпадающий список групп с инлайн-созданием новой по кнопке «+».
    [NoAutoStaticsCleanup]
    public static class CatalogGroupField {
        private const float AddButtonWidth = 22f;
        private const float OkButtonWidth = 32f;
        private const float CancelButtonWidth = 52f;

        private static CatalogGroupsRegistry _creatingFor;
        private static string _newGroupName = string.Empty;

        public static void Draw(
            CatalogGroupsRegistry registry,
            string currentGroup,
            bool mixed,
            Action<string> onSelected) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Group");

            if (ReferenceEquals(_creatingFor, registry))
                DrawCreate(registry, onSelected);
            else
                DrawPopup(registry, currentGroup, mixed, onSelected);

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawCreate(CatalogGroupsRegistry registry, Action<string> onSelected) {
            _newGroupName = EditorGUILayout.TextField(_newGroupName);

            if (GUILayout.Button("OK", EditorStyles.miniButton, GUILayout.Width(OkButtonWidth))) {
                if (registry.TryAddGroup(_newGroupName, out var groupName)) {
                    onSelected(groupName);
                    CancelCreate();
                }
            }

            if (GUILayout.Button("Cancel", EditorStyles.miniButton, GUILayout.Width(CancelButtonWidth)))
                CancelCreate();
        }

        private static void DrawPopup(
            CatalogGroupsRegistry registry,
            string currentGroup,
            bool mixed,
            Action<string> onSelected) {
            var groups = GetPopupGroups(registry, currentGroup);
            var index = IndexOf(groups, currentGroup);

            var changed = CatalogInspectorGUI.TryDraw(
                mixed,
                () => groups.Count == 0 ? -1 : EditorGUILayout.Popup(Mathf.Max(index, 0), groups.ToArray()),
                out var nextIndex);

            if (changed && nextIndex >= 0 && nextIndex < groups.Count)
                onSelected(groups[nextIndex]);

            if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(AddButtonWidth))) {
                _creatingFor = registry;
                _newGroupName = string.Empty;
                GUI.FocusControl(null);
            }
        }

        private static void CancelCreate() {
            _creatingFor = null;
            _newGroupName = string.Empty;
        }

        private static List<string> GetPopupGroups(CatalogGroupsRegistry registry, string currentGroup) {
            var groups = new List<string>(registry.GetGroups());
            if (string.IsNullOrEmpty(currentGroup) == false && IndexOf(groups, currentGroup) < 0)
                groups.Add(currentGroup);

            return groups;
        }

        private static int IndexOf(IReadOnlyList<string> groups, string group) {
            for (var i = 0; i < groups.Count; i++) {
                if (string.Equals(groups[i], group, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }
    }
}
