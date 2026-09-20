using UnityEditor;
using UnityEngine;

namespace Global.UI
{
    /// <summary>Shared inspector widgets for the design layouts: enums are drawn as button rows, not dropdowns.</summary>
    internal static class DesignLayoutEditorGUI
    {
        private const float RowHeight = 20f;
        private const float IconButtonWidth = 32f;

        public static void Header(string title)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        /// <summary>Draws an enum property as a row of buttons.</summary>
        public static void Toolbar(SerializedProperty property, GUIContent label, GUIContent[] options, bool compact = false)
        {
            var rect = EditorGUILayout.GetControlRect(true, RowHeight);
            var content = EditorGUI.PrefixLabel(rect, label);

            if (compact)
                content.width = Mathf.Min(content.width, IconButtonWidth * options.Length);

            using var scope = new EditorGUI.PropertyScope(rect, label, property);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

            var selected = GUI.Toolbar(content, property.enumValueIndex, options);

            EditorGUI.showMixedValue = false;

            if (EditorGUI.EndChangeCheck())
                property.enumValueIndex = selected;
        }

        /// <summary>Built in editor icon, falling back to the text when the icon is not available.</summary>
        public static GUIContent Icon(string iconName, string text, string tooltip)
        {
            var content = EditorGUIUtility.IconContent(iconName);

            if (content?.image == null)
                return new GUIContent(text, tooltip);

            return new GUIContent(content.image, tooltip);
        }

        public static GUIContent Text(string text, string tooltip) => new(text, tooltip);

        public static void DrawScriptField(SerializedObject serializedObject)
        {
            var script = serializedObject.FindProperty("m_Script");

            if (script == null)
                return;

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(script);
        }
    }
}
