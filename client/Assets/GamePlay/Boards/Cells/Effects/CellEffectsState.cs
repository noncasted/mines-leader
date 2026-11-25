using System;
using Internal;
using Shared;

namespace GamePlay.Boards.Effects
{
    public class CellEffectsState : ICellEffectsState
    {
        private readonly ViewableDictionary<Guid, CellEffectType> _active = new();

        public IViewableDictionary<Guid, CellEffectType> Active => _active;

        public void Add(Guid effectId, CellEffectType type)
        {
            // One card play gives one effect id to a set of distinct cells, so a cell never gets the same id twice
            // while it is active: a repeat means a broken sync.
            if (_active.ContainsKey(effectId) == true)
                throw new InvalidOperationException($"Effect {effectId} ({type}) is already active on the cell");

            _active.Add(effectId, type);
        }

        public void Remove(Guid effectId)
        {
            // A change of the cell status clears its effects, and the server still sends the removal
            // of the effect for every cell it was put on, so an unknown id is expected here.
            if (_active.ContainsKey(effectId) == false)
                return;

            _active.Remove(effectId);
        }

        public void Clear()
        {
            _active.Clear();
        }
    }
}
