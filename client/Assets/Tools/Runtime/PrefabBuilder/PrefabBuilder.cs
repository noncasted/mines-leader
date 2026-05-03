#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tools.Runtime.PrefabBuilder
{
    public class PrefabBuilder
    {
        public readonly GameObject GameObject;
        public readonly List<BuilderSerializedAction> SerializedActions = new();
        public readonly List<PrefabBuilder> Children = new();
        public readonly Dictionary<Type, object> Components = new();
        public readonly PrefabBuilderContainer Container;

        public PrefabBuilder()
        {
            GameObject = new GameObject();
            Container = new PrefabBuilderContainer();
        }

        private PrefabBuilder(GameObject gameObject, PrefabBuilderContainer container)
        {
            GameObject = gameObject;
            Container = container;
        }

        public static PrefabBuilder FromGameObject(GameObject root)
        {
            return new PrefabBuilder(root, new PrefabBuilderContainer());
        }

        public static PrefabBuilder FromGameObject(GameObject root, PrefabBuilderContainer container)
        {
            return new PrefabBuilder(root, container);
        }

        public string PrefabPath { get; set; }
        public string PrefabName => PrefabPath ?? GameObject.name;
    }
}
#endif