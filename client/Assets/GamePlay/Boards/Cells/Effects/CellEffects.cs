using System;
using System.Collections.Generic;
using System.Linq;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Boards.Effects
{
    public interface ICellEffects
    {
        IReadOnlyCollection<CellEffectType> Active { get; }

        void Add(Guid effectId, CellEffectType type);
        void Remove(Guid effectId);
    }

    [DisallowMultipleComponent]
    public class CellEffects : MonoBehaviour, ICellEffects
    {
        [SerializeField] private Dictionary<CellEffectType, CellEffect> _effects;

        private readonly Dictionary<Guid, ActiveEffect> _active = new();

        public IReadOnlyCollection<CellEffectType> Active => _active.Values.Select(effect => effect.Type).ToArray();

        private void Awake()
        {
            foreach (var (_, effect) in _effects)
                effect.gameObject.SetActive(false);
        }

        public void Add(Guid effectId, CellEffectType type)
        {
            // One card play gives one effect id to a set of distinct cells, so a cell never gets the same id twice
            // while it is active: a repeat means a broken sync.
            if (_active.ContainsKey(effectId) == true)
            {
                throw new InvalidOperationException(
                    $"Effect {effectId} ({type}) is already active on cell {transform.parent.name}");
            }

            if (_effects.TryGetValue(type, out var effect) == false)
            {
                throw new InvalidOperationException(
                    $"Cell {transform.parent.name} has no view of effect {type} ({effectId})");
            }

            var lifetime = this.GetObjectLifetime().Child();
            _active.Add(effectId, new ActiveEffect(type, lifetime));
            effect.Activate(lifetime);
        }

        public void Remove(Guid effectId)
        {
            // A change of the cell status clears its effects, and the server still sends the removal
            // of the effect for every cell it was put on, so an unknown id is expected here.
            if (_active.Remove(effectId, out var active) == false)
                return;

            active.Lifetime.Terminate();
        }

        public void Clear()
        {
            foreach (var (_, active) in _active)
                active.Lifetime.Terminate();

            _active.Clear();
        }

        private readonly struct ActiveEffect
        {
            public ActiveEffect(CellEffectType type, ILifetime lifetime)
            {
                Type = type;
                Lifetime = lifetime;
            }

            public CellEffectType Type { get; }
            public ILifetime Lifetime { get; }
        }
    }
}
