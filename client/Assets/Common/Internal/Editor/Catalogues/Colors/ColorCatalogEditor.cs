using System.Collections.Generic;
using System.Text;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Internal
{
    [CustomEditor(typeof(ColorCatalog))]
    [AutoStaticsCleanup]
    public sealed partial class ColorCatalogEditor : ButtonsInspector
    {
        private const float HandleWidth = 14f;
        private const float NameWidth = 0.45f;
        private const float Gap = 4f;
        private const float Indent = 12f;

        private SerializedProperty _groups;
        private ReorderableList _groupsList;

        private readonly Dictionary<string, ReorderableList> _entryLists = new();

        private static GUIStyle _hintStyle;

        private static float LineHeight => EditorGUIUtility.singleLineHeight;
        private static float Spacing => EditorGUIUtility.standardVerticalSpacing;

        protected override void OnEnable()
        {
            base.OnEnable();

            _groups = serializedObject.FindProperty("_groups");
            _entryLists.Clear();

            _groupsList = new ReorderableList(serializedObject, _groups, true, true, true, true)
            {
                drawHeaderCallback = DrawGroupsHeader,
                elementHeightCallback = GetGroupHeight,
                drawElementCallback = DrawGroup,
                onAddCallback = AddGroup,
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _groupsList.DoLayoutList();

            DrawIssues();
            DrawButtons();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGroupsHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, $"Groups ({_groups.arraySize})", EditorStyles.boldLabel);
        }

        private float GetGroupHeight(int index)
        {
            SerializedProperty group = _groups.GetArrayElementAtIndex(index);
            float height = LineHeight + Spacing * 2f;

            if (group.isExpanded)
                height += GetEntryList(group).GetHeight() + Spacing;

            return height;
        }

        private void DrawGroup(Rect rect, int index, bool active, bool focused)
        {
            SerializedProperty group = _groups.GetArrayElementAtIndex(index);
            SerializedProperty name = group.FindPropertyRelative(nameof(ColorGroup.Name));
            SerializedProperty entries = group.FindPropertyRelative(nameof(ColorGroup.Entries));

            var headerRect = new Rect(rect.x, rect.y + Spacing, rect.width, LineHeight);
            var foldoutRect = new Rect(headerRect.x, headerRect.y, HandleWidth, headerRect.height);

            float hintWidth = headerRect.width * 0.5f;
            float nameWidth = headerRect.width - HandleWidth - hintWidth - Gap;

            var nameRect = new Rect(headerRect.x + HandleWidth, headerRect.y, nameWidth, headerRect.height);
            var hintRect = new Rect(nameRect.xMax + Gap, headerRect.y, hintWidth, headerRect.height);

            group.isExpanded = EditorGUI.Foldout(foldoutRect, group.isExpanded, GUIContent.none);

            name.stringValue = EditorGUI.DelayedTextField(nameRect, name.stringValue);

            string identifier = ColorGenerator.ToIdentifier(name.stringValue);
            string hint = string.IsNullOrEmpty(identifier)
                ? $"no name · {entries.arraySize}"
                : $"Colors.{identifier} · {entries.arraySize}";

            _hintStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            EditorGUI.LabelField(hintRect, hint, _hintStyle);

            if (group.isExpanded == false)
                return;

            var entriesRect = new Rect(
                rect.x + Indent,
                headerRect.yMax + Spacing,
                rect.width - Indent,
                GetEntryList(group).GetHeight());

            GetEntryList(group).DoList(entriesRect);
        }

        private void AddGroup(ReorderableList list)
        {
            int index = _groups.arraySize;
            _groups.InsertArrayElementAtIndex(index);

            SerializedProperty group = _groups.GetArrayElementAtIndex(index);
            group.FindPropertyRelative(nameof(ColorGroup.Name)).stringValue = string.Empty;
            group.FindPropertyRelative(nameof(ColorGroup.Entries)).ClearArray();
            group.isExpanded = true;

            list.index = index;
        }

        private ReorderableList GetEntryList(SerializedProperty group)
        {
            SerializedProperty entries = group.FindPropertyRelative(nameof(ColorGroup.Entries));

            if (_entryLists.TryGetValue(group.propertyPath, out ReorderableList list) == false)
            {
                list = new ReorderableList(serializedObject, entries, true, false, true, true)
                {
                    headerHeight = 2f,
                    elementHeight = LineHeight + Gap,
                    onAddCallback = AddEntry,
                };

                list.drawElementCallback = (rect, index, active, focused) =>
                    DrawEntry(list.serializedProperty, rect, index);

                _entryLists[group.propertyPath] = list;
            }

            // Array operations invalidate cached properties, so keep the list pointed at a fresh one.
            list.serializedProperty = entries;
            return list;
        }

        private static void DrawEntry(SerializedProperty entries, Rect rect, int index)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            SerializedProperty name = entry.FindPropertyRelative(nameof(ColorEntry.Name));
            SerializedProperty value = entry.FindPropertyRelative(nameof(ColorEntry.Value));

            var row = new Rect(rect.x, rect.y + Gap * 0.5f, rect.width, LineHeight);

            float nameWidth = row.width * NameWidth;
            var nameRect = new Rect(row.x, row.y, nameWidth - Gap, row.height);
            var valueRect = new Rect(row.x + nameWidth, row.y, row.width - nameWidth, row.height);

            name.stringValue = EditorGUI.DelayedTextField(nameRect, name.stringValue);
            value.colorValue = EditorGUI.ColorField(valueRect, GUIContent.none, value.colorValue, true, true, false);
        }

        private static void AddEntry(ReorderableList list)
        {
            SerializedProperty entries = list.serializedProperty;
            int index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);

            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative(nameof(ColorEntry.Name)).stringValue = string.Empty;
            entry.FindPropertyRelative(nameof(ColorEntry.Value)).colorValue = Color.white;

            list.index = index;
        }

        private void DrawIssues()
        {
            var issues = new StringBuilder();
            var groupNames = new HashSet<string>();

            for (var groupIndex = 0; groupIndex < _groups.arraySize; groupIndex++)
            {
                SerializedProperty group = _groups.GetArrayElementAtIndex(groupIndex);
                string groupName = group.FindPropertyRelative(nameof(ColorGroup.Name)).stringValue;
                string groupIdentifier = ColorGenerator.ToIdentifier(groupName);

                if (string.IsNullOrEmpty(groupIdentifier))
                    issues.AppendLine($"Group {groupIndex}: name is empty");
                else if (groupNames.Add(groupIdentifier) == false)
                    issues.AppendLine($"Group {groupIndex}: duplicate of Colors.{groupIdentifier}");

                SerializedProperty entries = group.FindPropertyRelative(nameof(ColorGroup.Entries));
                var entryNames = new HashSet<string>();

                for (var entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(entryIndex);
                    string entryName = entry.FindPropertyRelative(nameof(ColorEntry.Name)).stringValue;
                    string entryIdentifier = ColorGenerator.ToIdentifier(entryName);

                    if (string.IsNullOrEmpty(entryIdentifier))
                        issues.AppendLine($"{groupName}: color {entryIndex} has no name");
                    else if (entryNames.Add(entryIdentifier) == false)
                        issues.AppendLine($"{groupName}: duplicate color '{entryIdentifier}'");
                }
            }

            if (issues.Length == 0)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(issues.ToString().TrimEnd(), MessageType.Warning);
        }
    }
}
