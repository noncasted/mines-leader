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
        [SerializeField] private CellEffectsDictionary _effects;

        private readonly Dictionary<Guid, ILifetime> _active = new();

        public void AddEffect(Guid effectId, CellEffectType cellEffectType)
        {
            var lifetime = this.GetObjectLifetime().Child();
            _active[effectId] = lifetime;
            var effect = _effects[cellEffectType];
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