using UnityEditor;
using UnityEngine;

namespace Internal
{
    [CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]
    public sealed class MinMaxSliderDrawer : PropertyDrawer
    {
        private const float SliderPadding = 5f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUI.GetPropertyHeight(property, label);

            if (property.propertyType != SerializedPropertyType.Vector2)
                height += EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;

            return height;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, property);

            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                DrawFallback(rect, property, label);
                EditorGUI.EndProperty();
                return;
            }

            var slider = (MinMaxSliderAttribute)attribute;

            float indentLength = EditorGUI.IndentedRect(rect).x - rect.x;
            float labelWidth = EditorGUIUtility.labelWidth + 2f;
            float fieldWidth = EditorGUIUtility.fieldWidth;
            float sliderWidth = rect.width - labelWidth - 2f * fieldWidth;

            var labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);

            var minFieldRect = new Rect(
                rect.x + labelWidth - indentLength,
                rect.y,
                fieldWidth + indentLength,
                rect.height);

            var sliderRect = new Rect(
                rect.x + labelWidth + fieldWidth + SliderPadding - indentLength,
                rect.y,
                sliderWidth - 2f * SliderPadding + indentLength,
                rect.height);

            var maxFieldRect = new Rect(
                rect.x + labelWidth + fieldWidth + sliderWidth - indentLength,
                rect.y,
                fieldWidth + indentLength,
                rect.height);

            EditorGUI.LabelField(labelRect, label);

            EditorGUI.BeginChangeCheck();

            Vector2 value = property.vector2Value;

            EditorGUI.MinMaxSlider(sliderRect, ref value.x, ref value.y, slider.MinValue, slider.MaxValue);

            value.x = EditorGUI.FloatField(minFieldRect, value.x);
            value.x = Mathf.Clamp(value.x, slider.MinValue, Mathf.Min(slider.MaxValue, value.y));

            value.y = EditorGUI.FloatField(maxFieldRect, value.y);
            value.y = Mathf.Clamp(value.y, Mathf.Max(slider.MinValue, value.x), slider.MaxValue);

            if (EditorGUI.EndChangeCheck())
                property.vector2Value = value;

            EditorGUI.EndProperty();
        }

        private static void DrawFallback(Rect rect, SerializedProperty property, GUIContent label)
        {
            float helpBoxHeight = EditorGUIUtility.singleLineHeight * 2f;

            var helpBoxRect = new Rect(rect.x, rect.y, rect.width, helpBoxHeight);
            EditorGUI.HelpBox(helpBoxRect, $"{nameof(MinMaxSliderAttribute)} can be used only on Vector2 fields", MessageType.Warning);

            var propertyRect = new Rect(
                rect.x,
                rect.y + helpBoxHeight + EditorGUIUtility.standardVerticalSpacing,
                rect.width,
                EditorGUI.GetPropertyHeight(property, label));

            EditorGUI.PropertyField(propertyRect, property, label, true);
        }
    }
}
