using System;
using System.Collections.Generic;
using GamePlay.Boards.Effects;
using Shared;

namespace GamePlay.Tests
{
    // The effects of a FakeCell by the rules of CellEffects: a repeated id throws, an unknown id on removal is ignored.
    public class FakeCellEffects : ICellEffects
    {
        private readonly Dictionary<Guid, CellEffectType> _active = new();

        public IReadOnlyCollection<CellEffectType> Active => new List<CellEffectType>(_active.Values);

        public void Add(Guid effectId, CellEffectType type)
        {
            if (_active.ContainsKey(effectId) == true)
                throw new InvalidOperationException($"Effect {effectId} ({type}) is already active");

            _active.Add(effectId, type);
        }

        public void Remove(Guid effectId)
        {
            _active.Remove(effectId);
        }

        public void Clear()
        {
            _active.Clear();
        }
    }
}
