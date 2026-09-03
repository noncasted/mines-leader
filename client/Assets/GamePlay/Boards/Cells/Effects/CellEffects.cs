using System;
using System.Collections.Generic;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Boards.Effects
{
    [DisallowMultipleComponent]
    public class CellEffects : MonoBehaviour
    {
        [SerializeField] private Dictionary<CellEffectType, CellEffect> _effects;

        private readonly Dictionary<Guid, ILifetime> _active = new();

        private void Awake()
        {
            foreach (var (_, effect) in _effects)
                effect.gameObject.SetActive(false);
        }

        public void AddEffect(Guid effectId, CellEffectType cellEffectType)
        {
            if (_effects.TryGetValue(cellEffectType, out var effect) == false)
            {
                Debug.LogWarning($"[CellEffects] Missing effect prefab for {cellEffectType}");
                return;
            }

            var lifetime = this.GetObjectLifetime().Child();
            _active[effectId] = lifetime;
            effect.Activate(lifetime);
        }

        public void RemoveEffect(Guid effectId)
        {
            if (_active.TryGetValue(effectId, out var lifetime))
            {
                lifetime.Terminate();
                _active.Remove(effectId);
            }
        }

        public void Clear()
        {
            foreach (var (_, lifetime) in _active)
                lifetime.Terminate();
        }
    }

    [Serializable]
    public class CellEffectsDictionary : SerializableDictionary<CellEffectType, CellEffect>
    {
    }
}