using System;
using System.Collections.Generic;

namespace Menu.Social
{
    public interface IMenuPlayersCollection
    {
        IReadOnlyDictionary<Guid, IMenuPlayer> Entries { get; }

        void Add(IMenuPlayer player);
    }
    
    public class MenuPlayersCollection : IMenuPlayersCollection
    {
        private readonly Dictionary<Guid, IMenuPlayer> _entries = new();

        public IReadOnlyDictionary<Guid, IMenuPlayer> Entries => _entries;

        public void Add(IMenuPlayer player)
        {
            _entries.Add(player.Id, player);
            player.Lifetime.Listen(() => _entries.Remove(player.Id));
        }
    }
}