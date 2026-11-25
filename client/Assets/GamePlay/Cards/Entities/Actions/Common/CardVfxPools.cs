using System;
using System.Collections.Generic;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    // One PrefabPool per VFX prefab under one parent. An instance is injected once when created and takes the layer
    // of the parent, so it renders on the camera of that parent (the board of the match or of the menu preview).
    public sealed class CardVfxPools
    {
        public CardVfxPools(IReadOnlyLifetime lifetime, Transform parent, IViewInjector viewInjector)
        {
            _lifetime = lifetime;
            _parent = parent;
            _viewInjector = viewInjector;
        }

        private readonly IReadOnlyLifetime _lifetime;
        private readonly Transform _parent;
        private readonly IViewInjector _viewInjector;
        private readonly Dictionary<MonoBehaviour, object> _pools = new();

        public T Get<T>(IReadOnlyLifetime lifetime, T prefab, Vector2 position, float angle) where T : MonoBehaviour
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab), $"VFX prefab of type {typeof(T).Name} is not set");

            if (_pools.TryGetValue(prefab, out var pool) == false)
            {
                pool = new PrefabPool<T>(_lifetime, prefab, _parent, OnCreated);
                _pools.Add(prefab, pool);
            }

            var instance = ((PrefabPool<T>)pool).Get(lifetime);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, angle));

            return instance;
        }

        private void OnCreated<T>(T instance) where T : MonoBehaviour
        {
            SetLayerRecursively(instance.transform, _parent.gameObject.layer);
            _viewInjector.Inject(instance);
        }

        private static void SetLayerRecursively(Transform target, int layer)
        {
            target.gameObject.layer = layer;

            for (var i = 0; i < target.childCount; i++)
                SetLayerRecursively(target.GetChild(i), layer);
        }
    }
}
