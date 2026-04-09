#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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
        public GameObject GameObject => _gameObject;

        public PrefabBuilder WithName(string name)
        {
            _name = name;
            _gameObject.name = name;
            return this;
        }

        public PrefabBuilder WithRectTransform(Action<RectTransform> configure = null)
        {
            var rt = _gameObject.GetComponent<RectTransform>();
            if (rt == null)
                rt = _gameObject.AddComponent<RectTransform>();
            configure?.Invoke(rt);
            return this;
        }

        public PrefabBuilder WithPosition(float x, float y, float z)
        {
            _gameObject.transform.localPosition = new Vector3(x, y, z);
            return this;
        }

        public PrefabBuilder WithScale(float x, float y, float z)
        {
            _gameObject.transform.localScale = new Vector3(x, y, z);
            return this;
        }

        public PrefabBuilder WithRotation(float x, float y, float z)
        {
            _gameObject.transform.localEulerAngles = new Vector3(x, y, z);
            return this;
        }

        public static T LoadAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[PrefabBuilder] Asset not found at '{path}'");
            }

            return asset;
        }

        public static T LoadSubAsset<T>(string path, string subAssetName) where T : Object
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in allAssets)
            {
                if (asset is T typed && asset.name == subAssetName)
                    return typed;
            }

            Debug.LogWarning($"[PrefabBuilder] Sub-asset '{subAssetName}' not found at '{path}'");
            return null;
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

        public PrefabBuilder WithActive(bool active)
        {
            _gameObject.SetActive(active);
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

        public PrefabBuilder WithChildObject(string name, bool active, Action<PrefabBuilder> configure)
        {
            var childGo = new GameObject(name);
            childGo.transform.SetParent(_gameObject.transform, false);
            var childBuilder = new PrefabBuilder(childGo);
            configure(childBuilder);
            childGo.SetActive(active);
            _children.Add(childBuilder);
            return this;
        }

        public GameObject WithPrefabChild(string assetPath, string name = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogError($"[PrefabBuilder] Prefab not found at '{assetPath}'");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (name != null) instance.name = name;
            instance.transform.SetParent(_gameObject.transform, false);
            return instance;
        }

        public GameObject Build(string outputPath)
        {
            ApplyAllSerializedProperties();

            var prefab = PrefabUtility.SaveAsPrefabAsset(_gameObject, outputPath);
            Object.DestroyImmediate(_gameObject);
            return prefab;
        }

        private void ApplyAllSerializedProperties()
        {
            ApplySerializedProperties();
            foreach (var child in _children)
            {
                child.ApplyAllSerializedProperties();
            }
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
                    if (value is Object obj) property.objectReferenceValue = obj;
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.AnimationCurve:
                    if (value is AnimationCurve curve) property.animationCurveValue = curve;
                    break;
                case SerializedPropertyType.Vector4:
                    if (value is Vector4 v4) property.vector4Value = v4;
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