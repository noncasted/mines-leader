using System;
using Internal;
using Shared;

namespace GamePlay.Boards.Effects
{
    // The effects put on a cell by card plays, by the id of the play. A change of the cell status clears them.
    public interface ICellEffectsState
    {
        // Every entry lives until its effect is removed or the state is cleared.
        IViewableDictionary<Guid, CellEffectType> Active { get; }

        void Add(Guid effectId, CellEffectType type);
        void Remove(Guid effectId);
        void Clear();
    }
}
