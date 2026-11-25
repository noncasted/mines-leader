using System;
using System.Collections.Generic;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Boards.Effects
{
    // The view of the effects of a cell: an effect is shown while its entry is in the state.
    [DisallowMultipleComponent]
    public class CellEffects : MonoBehaviour
    {
        [SerializeField] private Dictionary<CellEffectType, CellEffect> _effects;

        private void Awake()
        {
            foreach (var (_, effect) in _effects)
                effect.gameObject.SetActive(false);
        }

        public void View(IReadOnlyLifetime lifetime, ICellEffectsState state)
        {
            // Every effect the server can put must have a view: checked once here, so a missing one fails the
            // setup of the board instead of a handler of the state.
            foreach (CellEffectType type in Enum.GetValues(typeof(CellEffectType)))
            {
                if (_effects.ContainsKey(type) == false)
                    throw new InvalidOperationException($"Cell {transform.parent.name} has no view of effect {type}");
            }

            // The entry lifetime ends with the removal of the effect, a clear of the state or the given lifetime.
            state.Active.View(lifetime, (entryLifetime, _, type) => _effects[type].Activate(entryLifetime));
        }
    }
}
