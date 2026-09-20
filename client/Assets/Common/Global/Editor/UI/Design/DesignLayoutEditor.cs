using UnityEditor;
using UnityEngine;

namespace Global.UI
{
    /// <summary>
    /// Base inspector for the design layouts. Keeps the common blocks (fit, pivot, refresh, padding)
    /// identical between the container and the grid, and fixes the order of the sections.
    /// </summary>
    internal abstract class DesignLayoutEditor : Editor
    {
        private static readonly GUIContent[] FitOptions =
        {
            DesignLayoutEditorGUI.Text("None", "The container size is not touched."),
            DesignLayoutEditorGUI.Text("Expand", "Grow the container when the content does not fit."),
            DesignLayoutEditorGUI.Text("Shrink", "Shrink the container down to the content."),
            DesignLayoutEditorGUI.Text("Both", "Always match the container size to the content.")
        };

        private static readonly GUIContent[] RefreshOptions =
        {
            DesignLayoutEditorGUI.Text("On Change", "Rebuild when the container, its children or their sizes change."),
            DesignLayoutEditorGUI.Text("Every Frame", "Rebuild every late update."),
            DesignLayoutEditorGUI.Text("Manual", "Rebuild only on an explicit Rebuild() call.")
        };

        private GUIContent[] _horizontalPivotOptions;
        private GUIContent[] _verticalPivotOptions;

        private SerializedProperty _padding;
        private SerializedProperty _horizontalPivot;
        private SerializedProperty _verticalPivot;
        private SerializedProperty _refreshMode;

        protected abstract string MainSectionTitle { get; }

        /// <summary>Everything that is specific to the concrete layout. Drawn first, right after the script field.</summary>
        protected abstract void DrawLayoutSection();

        /// <summary>The two container fit properties, in width then height order.</summary>
        protected abstract void DrawFitSection();

        protected virtual void OnEnable()
        {
            _padding = serializedObject.FindProperty("_padding");
            _horizontalPivot = serializedObject.FindProperty("_horizontalPivot");
            _verticalPivot = serializedObject.FindProperty("_verticalPivot");
            _refreshMode = serializedObject.FindProperty("_refreshMode");

            _horizontalPivotOptions = new[]
            {
                DesignLayoutEditorGUI.Icon("align_horizontally_left", "Left", "Left"),
                DesignLayoutEditorGUI.Icon("align_horizontally_center", "Center", "Center"),
                DesignLayoutEditorGUI.Icon("align_horizontally_right", "Right", "Right")
            };

            _verticalPivotOptions = new[]
            {
                DesignLayoutEditorGUI.Icon("align_vertically_top", "Top", "Top"),
                DesignLayoutEditorGUI.Icon("align_vertically_center", "Middle", "Middle"),
                DesignLayoutEditorGUI.Icon("align_vertically_bottom", "Bottom", "Bottom")
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DesignLayoutEditorGUI.DrawScriptField(serializedObject);

            DesignLayoutEditorGUI.Header(MainSectionTitle);
            DrawLayoutSection();

            DesignLayoutEditorGUI.Header("Fit");
            DrawFitSection();

            DesignLayoutEditorGUI.Header("Pivot");
            DrawPivotSection();

            DesignLayoutEditorGUI.Header("Refresh");
            DesignLayoutEditorGUI.Toolbar(_refreshMode, DesignLayoutEditorGUI.Text("Mode", "When the layout is rebuilt."), RefreshOptions);

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(_padding, DesignLayoutEditorGUI.Text("Padding", "Inner offsets of the container."), true);

            serializedObject.ApplyModifiedProperties();
        }

        protected void DrawFit(SerializedProperty property, string label, string tooltip)
        {
            DesignLayoutEditorGUI.Toolbar(property, DesignLayoutEditorGUI.Text(label, tooltip), FitOptions);
        }

        protected virtual void DrawPivotSection()
        {
            DesignLayoutEditorGUI.Toolbar(_horizontalPivot, HorizontalPivotLabel(), _horizontalPivotOptions, compact: true);
            DesignLayoutEditorGUI.Toolbar(_verticalPivot, VerticalPivotLabel(), _verticalPivotOptions, compact: true);
        }

        protected virtual GUIContent HorizontalPivotLabel() =>
            DesignLayoutEditorGUI.Text("Horizontal", "Horizontal alignment of the content.");

        protected virtual GUIContent VerticalPivotLabel() =>
            DesignLayoutEditorGUI.Text("Vertical", "Vertical alignment of the content.");
    }
}
