using UnityEditor;
using UnityEngine;

namespace Internal
{
    [CustomPropertyDrawer(typeof(CurveRangeAttribute))]
    public sealed class CurveRangeDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUI.GetPropertyHeight(property, label);

            if (property.propertyType != SerializedPropertyType.AnimationCurve)
                height += EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;

            return height;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, property);

            if (property.propertyType != SerializedPropertyType.AnimationCurve)
            {
                DrawFallback(rect, property, label);
                EditorGUI.EndProperty();
                return;
            }

            var curveRange = (CurveRangeAttribute)attribute;

            var ranges = curveRange.HasRange
                ? new Rect(
                    curveRange.Min.x,
                    curveRange.Min.y,
                    curveRange.Max.x - curveRange.Min.x,
                    curveRange.Max.y - curveRange.Min.y)
                : default;

            EditorGUI.CurveField(rect, property, Color.green, ranges, label);

            EditorGUI.EndProperty();
        }

        private static void DrawFallback(Rect rect, SerializedProperty property, GUIContent label)
        {
            float helpBoxHeight = EditorGUIUtility.singleLineHeight * 2f;

            var helpBoxRect = new Rect(rect.x, rect.y, rect.width, helpBoxHeight);
            EditorGUI.HelpBox(helpBoxRect, $"Field {property.name} is not an AnimationCurve", MessageType.Warning);

            var propertyRect = new Rect(
                rect.x,
                rect.y + helpBoxHeight + EditorGUIUtility.standardVerticalSpacing,
                rect.width,
                EditorGUI.GetPropertyHeight(property, label));

            EditorGUI.PropertyField(propertyRect, property, label, true);
        }
    }
}
