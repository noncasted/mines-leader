using UnityEditor;
using UnityEngine;

namespace Global.UI
{
    [CustomEditor(typeof(DesignContainer))]
    [CanEditMultipleObjects]
    internal class DesignContainerEditor : DesignLayoutEditor
    {
        private static readonly GUIContent[] AxisOptions =
        {
            DesignLayoutEditorGUI.Text("Horizontal", "Lay the children out in a row."),
            DesignLayoutEditorGUI.Text("Vertical", "Lay the children out in a column.")
        };

        private static readonly GUIContent[] MainAxisOptions =
        {
            DesignLayoutEditorGUI.Text("Group", "Keep the item sizes, the gap is exactly the spacing, the block is aligned by the pivot."),
            DesignLayoutEditorGUI.Text("Distribute", "Keep the item sizes, split the free space evenly between the items."),
            DesignLayoutEditorGUI.Text("Fill", "Resize the items so the content fills the container.")
        };

        private static readonly GUIContent[] CrossAxisOptions =
        {
            DesignLayoutEditorGUI.Text("Keep", "The item keeps its own size across the axis."),
            DesignLayoutEditorGUI.Text("Stretch", "The item is stretched to the container minus padding.")
        };

        private SerializedProperty _axis;
        private SerializedProperty _mainAxis;
        private SerializedProperty _crossAxis;
        private SerializedProperty _spacing;
        private SerializedProperty _widthFit;
        private SerializedProperty _heightFit;

        protected override string MainSectionTitle => "Layout";

        private bool IsVertical => _axis.enumValueIndex == (int)DesignAxis.Vertical;

        protected override void OnEnable()
        {
            base.OnEnable();

            _axis = serializedObject.FindProperty("_axis");
            _mainAxis = serializedObject.FindProperty("_mainAxis");
            _crossAxis = serializedObject.FindProperty("_crossAxis");
            _spacing = serializedObject.FindProperty("_spacing");
            _widthFit = serializedObject.FindProperty("_widthFit");
            _heightFit = serializedObject.FindProperty("_heightFit");
        }

        protected override void DrawLayoutSection()
        {
            DesignLayoutEditorGUI.Toolbar(_axis, DesignLayoutEditorGUI.Text("Axis", "Direction the children are laid out in."), AxisOptions);
            DesignLayoutEditorGUI.Toolbar(_mainAxis, DesignLayoutEditorGUI.Text("Along Axis", "How the free space along the axis is used."), MainAxisOptions);
            DesignLayoutEditorGUI.Toolbar(_crossAxis, DesignLayoutEditorGUI.Text("Across Axis", "How the children are sized across the axis."), CrossAxisOptions);

            EditorGUILayout.PropertyField(_spacing, DesignLayoutEditorGUI.Text("Spacing", "Gap between the items, the minimum gap in Distribute."));
        }

        protected override void DrawFitSection()
        {
            DrawFit(_widthFit, "Width", "How the container width follows its content.");
            DrawFit(_heightFit, "Height", "How the container height follows its content.");
        }

        protected override GUIContent HorizontalPivotLabel() => IsVertical
            ? DesignLayoutEditorGUI.Text("Horizontal", "Aligns every item across the axis.")
            : DesignLayoutEditorGUI.Text("Horizontal", "Aligns the whole block along the axis. Right also reverses the order: the first child ends up on the right.");

        protected override GUIContent VerticalPivotLabel() => IsVertical
            ? DesignLayoutEditorGUI.Text("Vertical", "Aligns the whole block along the axis. Bottom also reverses the order: the first child ends up at the bottom.")
            : DesignLayoutEditorGUI.Text("Vertical", "Aligns every item across the axis.");
    }
}
