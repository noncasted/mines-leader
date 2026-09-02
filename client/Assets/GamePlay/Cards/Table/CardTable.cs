using System.Collections.Generic;
using Internal;

namespace GamePlay.Cards
{
    /// <summary>
    /// Cards the player has already played: they lie on the table in the dropped
    /// state until the server tells them to fly into the stash.
    /// </summary>
    public interface ICardTable
    {
        IReadOnlyList<ICard> Entries { get; }

        void Add(ICard card);
        IReadOnlyList<ICard> Collect();
    }

    public class CardTable : ICardTable
    {
        private readonly List<ICard> _entries = new();

        public IReadOnlyList<ICard> Entries => _entries;

        public void Add(ICard card)
        {
            // The card may already be gone by the time its drop visuals finish (scene teardown).
            if (card.Lifetime.IsTerminated == true)
                return;

            _entries.Add(card);
            card.Lifetime.Listen(() => _entries.Remove(card));
        }

        public IReadOnlyList<ICard> Collect()
        {
            var collected = _entries.ToArray();
            _entries.Clear();

            return collected;
        }
    }
}
