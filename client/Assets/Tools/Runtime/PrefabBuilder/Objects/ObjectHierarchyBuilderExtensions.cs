#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Tools.Runtime.PrefabBuilder
{
    public static class ObjectHierarchyBuilderExtensions
    {
        public static PrefabBuilder WithActive(this PrefabBuilder builder, bool active)
        {
            builder.GameObject.SetActive(active);
            return builder;
        }

        public static GameObject WithChild(this PrefabBuilder builder, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(builder.GameObject.transform, false);
            return child;
        }

        public static T WithChild<T>(this PrefabBuilder builder, string name) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(builder.GameObject.transform, false);
            return child.AddComponent<T>();
        }

        public static T WithChild<T>(this PrefabBuilder builder, string name, Action<T> configure) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(builder.GameObject.transform, false);
            var component = child.AddComponent<T>();
            configure?.Invoke(component);
            return component;
        }

        public static PrefabBuilder WithChildObject(
            this PrefabBuilder builder,
            string name,
            Action<PrefabBuilder> configure)
        {
            return builder.WithChildObject(name, builder.GameObject.transform, true, configure);
        }

        public static PrefabBuilder WithChildObject(
            this PrefabBuilder builder,
            string name,
            bool active,
            Action<PrefabBuilder> configure)
        {
            return builder.WithChildObject(name, builder.GameObject.transform, active, configure);
        }

        public static PrefabBuilder WithChild(this PrefabBuilder builder, Action<PrefabBuilder> configure)
        {
            var childGo = new GameObject("new object");
            childGo.transform.SetParent(builder.GameObject.transform, false);
            var childBuilder = PrefabBuilder.FromGameObject(childGo, builder.Container);
            configure(childBuilder);
            builder.Children.Add(childBuilder);
            return builder;
        }

        public static PrefabBuilder Disable(this PrefabBuilder builder)
        {
            builder.GameObject.SetActive(false);
            return builder;
        }

        public static PrefabBuilder WithChildObject(
            this PrefabBuilder builder,
            string name,
            Transform parent,
            Action<PrefabBuilder> configure)
        {
            return builder.WithChildObject(name, parent, true, configure);
        }

        public static PrefabBuilder WithChildObject(
            this PrefabBuilder builder,
            string name,
            Transform parent,
            bool active,
            Action<PrefabBuilder> configure)
        {
            var childGo = new GameObject(name);
            childGo.transform.SetParent(parent, false);
            var childBuilder = PrefabBuilder.FromGameObject(childGo, builder.Container);
            configure(childBuilder);
            childGo.SetActive(active);
            builder.Children.Add(childBuilder);
            return builder;
        }

        public static GameObject WithPrefabChild(this PrefabBuilder builder, string assetPath, string name = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefab == null)
            {
                Debug.LogError($"[PrefabBuilder] Prefab not found at '{assetPath}'");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            if (name != null)
                instance.name = name;
            instance.transform.SetParent(builder.GameObject.transform, false);
            return instance;
        }

        public static PrefabBuilder WithName(this PrefabBuilder builder, string name)
        {
            builder.PrefabPath = name;

            builder.GameObject.name = name.Contains("/")
                ? name.Substring(name.LastIndexOf('/') + 1)
                : name;

            return builder;
        }
    }
}
#endif