using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tools
{
    public static class SerializationBuilderExtensions
    {
        public static PrefabBuilder SetSerialized<T>(this PrefabBuilder builder, string fieldName, object value)
            where T : Component
        {
            var target = builder.GameObject.GetComponent<T>();

            if (target == null)
            {
                Debug.LogError(
                        $"[PrefabBuilder] Component {typeof(T).Name} not found on '{builder.GameObject.name}'. Add it with WithComponent first."
                    );
                return builder;
            }

            builder.SerializedActions.Add(new BuilderSerializedAction(target, fieldName, value));
            return builder;
        }

        public static GameObject Build(this PrefabBuilder builder, string outputPath)
        {
#if UNITY_EDITOR
            builder.Container.ResolveAll();
            ApplyAllSerializedProperties(builder);

            var prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(builder.GameObject, outputPath);
            Object.DestroyImmediate(builder.GameObject);
            return prefab;
#endif
            return null;
        }

        private static void ApplyAllSerializedProperties(PrefabBuilder builder)
        {
            ApplySerializedProperties(builder);

            foreach (var child in builder.Children)
            {
                ApplyAllSerializedProperties(child);
            }
        }

        private static void ApplySerializedProperties(PrefabBuilder builder)
        {
            if (builder.SerializedActions.Count == 0)
                return;

            var grouped = new Dictionary<Component, List<(string fieldName, object value)>>();

            foreach (var action in builder.SerializedActions)
            {
                if (!grouped.TryGetValue(action.Target, out var list))
                {
                    list = new List<(string, object)>();
                    grouped[action.Target] = list;
                }

                list.Add((action.FieldName, action.Value));
            }

            foreach (var (component, actions) in grouped)
            {
#if UNITY_EDITOR
                var so = new UnityEditor.SerializedObject(component);

                foreach (var (fieldName, value) in actions)
                {
                    var property = so.FindProperty(fieldName);

                    if (property == null)
                    {
                        Debug.LogWarning(
                                $"[PrefabBuilder] Property '{fieldName}' not found on {component.GetType().Name}"
                            );
                        continue;
                    }

                    SetPropertyValue(property, value);
                }

                so.ApplyModifiedPropertiesWithoutUndo();
#endif
            }
        }

#if UNITY_EDITOR
        private static void SetPropertyValue(UnityEditor.SerializedProperty property, object value)
        {
            switch (property.propertyType)
            {
                case UnityEditor.SerializedPropertyType.String:
                    property.stringValue = value as string ?? value.ToString();
                    break;
                case UnityEditor.SerializedPropertyType.Integer:
                    property.intValue = Convert.ToInt32(value);
                    break;
                case UnityEditor.SerializedPropertyType.Float:
                    property.floatValue = Convert.ToSingle(value);
                    break;
                case UnityEditor.SerializedPropertyType.Boolean:
                    property.boolValue = Convert.ToBoolean(value);
                    break;
                case UnityEditor.SerializedPropertyType.Color:
                    if (value is Color color)
                        property.colorValue = color;
                    break;
                case UnityEditor.SerializedPropertyType.Vector2:
                    if (value is Vector2 v2)
                        property.vector2Value = v2;
                    break;
                case UnityEditor.SerializedPropertyType.Vector3:
                    if (value is Vector3 v3)
                        property.vector3Value = v3;
                    break;
                case UnityEditor.SerializedPropertyType.ObjectReference:
                    if (value is Object obj)
                        property.objectReferenceValue = obj;
                    break;
                case UnityEditor.SerializedPropertyType.Enum:
                    property.enumValueIndex = Convert.ToInt32(value);
                    break;
                case UnityEditor.SerializedPropertyType.AnimationCurve:
                    if (value is AnimationCurve curve)
                        property.animationCurveValue = curve;
                    break;
                case UnityEditor.SerializedPropertyType.Vector4:
                    if (value is Vector4 v4)
                        property.vector4Value = v4;
                    break;
                default:
                    if (property.isArray && value is Array arr)
                    {
                        property.arraySize = arr.Length;

                        for (int i = 0; i < arr.Length; i++)
                        {
                            SetPropertyValue(property.GetArrayElementAtIndex(i), arr.GetValue(i));
                        }

                        break;
                    }

                    Debug.LogWarning(
                            $"[PrefabBuilder] Unsupported property type: {property.propertyType} for '{property.name}'"
                        );
                    break;
            }
        }
#endif
    }

    public readonly struct BuilderSerializedAction
    {
        public BuilderSerializedAction(Component target, string fieldName, object value)
        {
            Target = target;
            FieldName = fieldName;
            Value = value;
        }

        public readonly Component Target;
        public readonly string FieldName;
        public readonly object Value;
    }
}