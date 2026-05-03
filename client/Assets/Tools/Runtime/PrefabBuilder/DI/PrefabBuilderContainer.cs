#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tools.Runtime.PrefabBuilder
{
    public class PrefabBuilderContainer
    {
        private readonly Dictionary<Type, Dictionary<string, object>> _registry = new();
        private readonly List<Component> _targets = new();

        public void Register(object value, string key = "")
        {
            var type = value.GetType();
            RegisterForType(type, key, value);

            var baseType = type.BaseType;

            while (baseType != null &&
                   baseType != typeof(object) &&
                   baseType != typeof(Component) &&
                   baseType != typeof(MonoBehaviour))
            {
                RegisterForType(baseType, key, value);
                baseType = baseType.BaseType;
            }
        }

        public void Register(object value, Type asType, string key = "")
        {
            RegisterForType(asType, key, value);
        }

        public void AddTarget(Component target)
        {
            _targets.Add(target);
        }

        public void ResolveAll()
        {
            foreach (var target in _targets)
            {
                ResolveTarget(target);
            }
        }

        private void ResolveTarget(Component target)
        {
            var type = target.GetType();

            while (type != null && type != typeof(MonoBehaviour) && type != typeof(Component))
            {
                var fields = type.GetFields(BindingFlags.Instance |
                                            BindingFlags.NonPublic |
                                            BindingFlags.Public |
                                            BindingFlags.DeclaredOnly);

                foreach (var field in fields)
                {
                    var attr = field.GetCustomAttribute<InjectGeneratedAttribute>();

                    if (attr == null)
                        continue;

                    var value = Resolve(target, field.FieldType, attr.Key);
                    field.SetValue(target, value);
                }

                type = type.BaseType;
            }
        }

        private object Resolve(Component target, Type type, string key)
        {
            if (_registry.TryGetValue(type, out var byKey) && byKey.TryGetValue(key, out var value))
                return value;

            var keyInfo = string.IsNullOrEmpty(key) ? "without key" : $"with key '{key}'";

            throw new InvalidOperationException(
                    $"[PrefabBuilderContainer] Cannot resolve {type.Name} {keyInfo} for {target.GetType().Name}. " +
                    $"Registered types: [{string.Join(", ", _registry.Keys)}]"
                );
        }

        private void RegisterForType(Type type, string key, object value)
        {
            if (!_registry.TryGetValue(type, out var byKey))
            {
                byKey = new Dictionary<string, object>();
                _registry[type] = byKey;
            }

            byKey[key] = value;
        }
    }
}
#endif