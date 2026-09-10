using System;
using UnityEngine;

namespace Internal
{
    public sealed class PrefabEntry
    {
        public PrefabEntry(GameObject asset)
        {
            Asset = asset;
        }

        public GameObject Asset { get; }

        public T As<T>() where T : Component
        {
            EnsureAsset();
            var component = Asset.GetComponent<T>();

            if (component == null)
                throw new InvalidOperationException($"{Asset.name} has no {typeof(T).Name}");

            return component;
        }

        public static implicit operator GameObject(PrefabEntry entry)
        {
            return entry.Asset;
        }

        private void EnsureAsset()
        {
            if (Asset == null)
                throw new InvalidOperationException("PrefabEntry asset is null");
        }
    }
}