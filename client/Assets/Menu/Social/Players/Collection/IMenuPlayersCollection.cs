using System;
using System.Collections.Generic;

namespace Menu.Social
{
    public interface IMenuPlayersCollection
    {
        IReadOnlyDictionary<Guid, IMenuPlayer> Entries { get; }

        void Add(IMenuPlayer player);
    }
}