using System;
using UnityEngine;

namespace Tools.PrefabBuilder
{
    public static class ObjectComponentsBuilderExtensions
    {
        public static PrefabBuilder WithComponent<T>(this PrefabBuilder builder, out T component) where T : Component
        {
            component = builder.GameObject.AddComponent<T>();
            builder.Components.Add(typeof(T), component);
            builder.Container.AddTarget(component);
            return builder;
        }

        public static PrefabBuilder WithComponent<T>(this PrefabBuilder builder, Action<T> configure = null)
            where T : Component
        {
            var component = builder.GameObject.AddComponent<T>();
            configure?.Invoke(component);
            builder.Components.Add(typeof(T), component);
            builder.Container.AddTarget(component);
            return builder;
        }

        public static PrefabBuilder WithComponent<T>(this PrefabBuilder builder, string key)
            where T : Component
        {
            var component = builder.GameObject.AddComponent<T>();
            builder.Components.Add(typeof(T), component);
            builder.Container.AddTarget(component);
            builder.Register(component, key);
            return builder;
        }

        public static PrefabBuilder Register(this PrefabBuilder builder, object value, string key = "")
        {
            builder.Container.Register(value, key);
            return builder;
        }

        public static PrefabBuilder Register<T>(this PrefabBuilder builder, T value, string key = "")
        {
            builder.Container.Register(value, typeof(T), key);
            return builder;
        }

        public static T GetComponent<T>(this PrefabBuilder builder) where T : Component
        {
            if (builder.Components.TryGetValue(typeof(T), out var component) == true)
                return (T)component;

            foreach (var child in builder.Children)
            {
                if (child.TryGetComponent<T>(out var childComponent) != false)
                    return childComponent;
            }

            throw new Exception();
        }

        public static bool TryGetComponent<T>(this PrefabBuilder builder, out T component) where T : Component
        {
            component = null;

            if (builder.Components.TryGetValue(typeof(T), out var foundComponent) == true)
            {
                component = (T)foundComponent;
                return true;
            }

            foreach (var child in builder.Children)
            {
                if (child.TryGetComponent<T>(out var childComponent) == true)
                {
                    component = childComponent;
                    return true;
                }
            }

            return false;
        }
    }
}