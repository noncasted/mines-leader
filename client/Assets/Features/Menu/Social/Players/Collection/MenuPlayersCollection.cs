using System;
using System.Collections.Generic;

namespace Menu
{
    public class MenuPlayersCollection : IMenuPlayersCollection
    {
        private readonly Dictionary<Guid, IMenuPlayer> _entries = new();

        public IReadOnlyDictionary<Guid, IMenuPlayer> Entries => _entries;

        public void Add(IMenuPlayer player)
        {
            _entries.Add(player.PlayerId, player);
            player.Lifetime.Listen(() => _entries.Remove(player.PlayerId));
        }
    }
}