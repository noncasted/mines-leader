using UnityEditor;
using UnityEngine;

namespace Global.UI
{
    [CustomEditor(typeof(DesignItem))]
    [CanEditMultipleObjects]
    internal class DesignItemEditor : Editor
    {
        private static readonly GUIContent[] SizingOptions =
        {
            DesignLayoutEditorGUI.Text("Preferred", "Keep the size the child already has."),
            DesignLayoutEditorGUI.Text("Percent", "Take a fraction of the container size."),
            DesignLayoutEditorGUI.Text("Weight", "Share the free space with the other weighted items (Fill mode)."),
            DesignLayoutEditorGUI.Text("Ignore", "The child is not laid out at all.")
        };

        private static readonly GUIContent[] CrossAxisOptions =
        {
            DesignLayoutEditorGUI.Text("Keep", "The item keeps its own size across the axis."),
            DesignLayoutEditorGUI.Text("Stretch", "The item is stretched to the container minus padding.")
        };

        private SerializedProperty _sizing;
        private SerializedProperty _value;
        private SerializedProperty _overrideCrossAxis;
        private SerializedProperty _crossAxis;

        private void OnEnable()
        {
            _sizing = serializedObject.FindProperty("_sizing");
            _value = serializedObject.FindProperty("_value");
            _overrideCrossAxis = serializedObject.FindProperty("_overrideCrossAxis");
            _crossAxis = serializedObject.FindProperty("_crossAxis");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DesignLayoutEditorGUI.DrawScriptField(serializedObject);

            DesignLayoutEditorGUI.Header("Size");
            DesignLayoutEditorGUI.Toolbar(_sizing, DesignLayoutEditorGUI.Text("Sizing", "Size the item takes along the layout axis."), SizingOptions);

            switch ((DesignItemSizing)_sizing.enumValueIndex)
            {
                case DesignItemSizing.Percent:
                    EditorGUILayout.PropertyField(_value, DesignLayoutEditorGUI.Text("Percent", "Fraction of the container, 0 to 1."));
                    break;

                case DesignItemSizing.Weight:
                    EditorGUILayout.PropertyField(_value, DesignLayoutEditorGUI.Text("Weight", "Relative share of the free space."));
                    break;
            }

            DesignLayoutEditorGUI.Header("Across Axis");
            EditorGUILayout.PropertyField(_overrideCrossAxis, DesignLayoutEditorGUI.Text("Override", "Use an own cross axis mode instead of the container one."));

            using (new EditorGUI.DisabledScope(_overrideCrossAxis.boolValue == false))
                DesignLayoutEditorGUI.Toolbar(_crossAxis, DesignLayoutEditorGUI.Text("Mode", "How the item is sized across the axis."), CrossAxisOptions);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
