#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tools
{
    public class PrefabBuilder
    {
        private readonly GameObject _gameObject;
        private readonly List<SerializedAction> _serializedActions = new();
        private readonly List<PrefabBuilder> _children = new();

        private string _name = "Unnamed";

        public PrefabBuilder()
        {
            _gameObject = new GameObject();
        }

        private PrefabBuilder(GameObject gameObject)
        {
            _gameObject = gameObject;
        }

        public string PrefabName => _name;

        public PrefabBuilder WithName(string name)
        {
            _name = name;
            _gameObject.name = name;
            return this;
        }

        public PrefabBuilder WithComponent<T>(out T component) where T : Component
        {
            component = _gameObject.AddComponent<T>();
            return this;
        }

        public PrefabBuilder WithComponent<T>(Action<T> configure = null) where T : Component
        {
            var component = _gameObject.AddComponent<T>();
            configure?.Invoke(component);
            return this;
        }

        public GameObject WithChild(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_gameObject.transform, false);

            return child;
        }

        public T WithChild<T>(string name) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(_gameObject.transform, false);
            return child.AddComponent<T>();
        }

        public T WithChild<T>(string name, Action<T> configure) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(_gameObject.transform, false);
            var component = child.AddComponent<T>();
            
            configure?.Invoke(component);
            
            return component;
        }

        public PrefabBuilder SetSerialized<T>(string fieldName, object value) where T : Component
        {
            var target = _gameObject.GetComponent<T>();
            if (target == null)
            {
                Debug.LogError(
                    $"[PrefabBuilder] Component {typeof(T).Name} not found on '{_gameObject.name}'. Add it with WithComponent first."
                );
                return this;
            }

            _serializedActions.Add(new SerializedAction(target, fieldName, value));
            return this;
        }

        public PrefabBuilder WithChildObject(string name, Action<PrefabBuilder> configure)
        {
            var childGo = new GameObject(name);
            childGo.transform.SetParent(_gameObject.transform, false);
            var childBuilder = new PrefabBuilder(childGo);
            configure(childBuilder);
            _children.Add(childBuilder);
            return this;
        }

        public GameObject Build(string outputPath)
        {
            ApplySerializedProperties();
            foreach (var child in _children)
            {
                child.ApplySerializedProperties();
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(_gameObject, outputPath);
            UnityEngine.Object.DestroyImmediate(_gameObject);
            return prefab;
        }

        private void ApplySerializedProperties()
        {
            if (_serializedActions.Count == 0) return;

            // Group actions by target component
            var grouped = new Dictionary<Component, List<(string fieldName, object value)>>();
            foreach (var action in _serializedActions)
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
                var so = new SerializedObject(component);
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
            }
        }

        private static void SetPropertyValue(SerializedProperty property, object value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    property.stringValue = value as string ?? value.ToString();
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = Convert.ToBoolean(value);
                    break;
                case SerializedPropertyType.Color:
                    if (value is Color color) property.colorValue = color;
                    break;
                case SerializedPropertyType.Vector2:
                    if (value is Vector2 v2) property.vector2Value = v2;
                    break;
                case SerializedPropertyType.Vector3:
                    if (value is Vector3 v3) property.vector3Value = v3;
                    break;
                case SerializedPropertyType.ObjectReference:
                    if (value is UnityEngine.Object obj) property.objectReferenceValue = obj;
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = Convert.ToInt32(value);
                    break;
                default:
                    Debug.LogWarning(
                        $"[PrefabBuilder] Unsupported property type: {property.propertyType} for '{property.name}'"
                    );
                    break;
            }
        }

        private readonly struct SerializedAction
        {
            public SerializedAction(Component target, string fieldName, object value)
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
}
#endif