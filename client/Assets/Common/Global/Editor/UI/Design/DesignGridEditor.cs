using UnityEditor;
using UnityEngine;

namespace Global.UI
{
    [CustomEditor(typeof(DesignGrid))]
    [CanEditMultipleObjects]
    internal class DesignGridEditor : DesignLayoutEditor
    {
        private static readonly GUIContent[] FlowOptions =
        {
            DesignLayoutEditorGUI.Text("Horizontal", "The grid grows to the side: a column is filled top to bottom, then the next column is added."),
            DesignLayoutEditorGUI.Text("Vertical", "The grid grows downwards: a row is filled left to right, then the next row is added.")
        };

        private static readonly GUIContent[] ConstraintOptions =
        {
            DesignLayoutEditorGUI.Text("Flexible", "Line count across the flow is derived from the container size and the cell size."),
            DesignLayoutEditorGUI.Text("Columns", "Fixed column count."),
            DesignLayoutEditorGUI.Text("Rows", "Fixed row count.")
        };

        private static readonly GUIContent[] CellSizingOptions =
        {
            DesignLayoutEditorGUI.Text("Fixed", "Cells use the configured cell size."),
            DesignLayoutEditorGUI.Text("Fit Container", "Cells are stretched so the lines fill the container.")
        };

        private SerializedProperty _flow;
        private SerializedProperty _constraint;
        private SerializedProperty _constraintCount;
        private SerializedProperty _cellSizing;
        private SerializedProperty _cellSize;
        private SerializedProperty _spacing;
        private SerializedProperty _widthFit;
        private SerializedProperty _heightFit;

        protected override string MainSectionTitle => "Grid";

        protected override void OnEnable()
        {
            base.OnEnable();

            _flow = serializedObject.FindProperty("_flow");
            _constraint = serializedObject.FindProperty("_constraint");
            _constraintCount = serializedObject.FindProperty("_constraintCount");
            _cellSizing = serializedObject.FindProperty("_cellSizing");
            _cellSize = serializedObject.FindProperty("_cellSize");
            _spacing = serializedObject.FindProperty("_spacing");
            _widthFit = serializedObject.FindProperty("_widthFit");
            _heightFit = serializedObject.FindProperty("_heightFit");
        }

        protected override void DrawLayoutSection()
        {
            DesignLayoutEditorGUI.Toolbar(_flow, DesignLayoutEditorGUI.Text("Flow", "Direction the grid grows in as children are added."), FlowOptions);
            DesignLayoutEditorGUI.Toolbar(_constraint, DesignLayoutEditorGUI.Text("Constraint", "How the line count is decided."), ConstraintOptions);

            if (_constraint.enumValueIndex != (int)DesignGridConstraint.Flexible)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    var label = _constraint.enumValueIndex == (int)DesignGridConstraint.FixedColumnCount ? "Columns" : "Rows";
                    EditorGUILayout.PropertyField(_constraintCount, DesignLayoutEditorGUI.Text(label, "Number of lines."));
                }
            }

            DesignLayoutEditorGUI.Toolbar(_cellSizing, DesignLayoutEditorGUI.Text("Cell Sizing", "How a cell size is decided."), CellSizingOptions);

            var cellSizeTooltip = _cellSizing.enumValueIndex == (int)DesignGridCellSizing.Fixed
                ? "Size of a single cell."
                : "Reference size, still used by the Flexible constraint to count the lines.";

            EditorGUILayout.PropertyField(_cellSize, DesignLayoutEditorGUI.Text("Cell Size", cellSizeTooltip));
            EditorGUILayout.PropertyField(_spacing, DesignLayoutEditorGUI.Text("Spacing", "Gap between the cells."));
        }

        protected override void DrawFitSection()
        {
            DrawFit(_widthFit, "Width", "How the container width follows the grid.");
            DrawFit(_heightFit, "Height", "How the container height follows the grid.");
        }

        protected override GUIContent HorizontalPivotLabel() =>
            DesignLayoutEditorGUI.Text("Horizontal", "Aligns the whole grid. Right also reverses the columns: the first child ends up in the last column.");

        protected override GUIContent VerticalPivotLabel() =>
            DesignLayoutEditorGUI.Text("Vertical", "Aligns the whole grid. Bottom also reverses the rows: the first child ends up in the last row.");
    }
}
