using System;
using System.Collections.Generic;

namespace Menu
{
    public interface IMenuPlayersCollection
    {
        IReadOnlyDictionary<Guid, IMenuPlayer> Entries { get; }

        void Add(IMenuPlayer player);
    }
}