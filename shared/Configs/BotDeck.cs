using System.Collections.Generic;

namespace Shared
{
    public class BotDeck
    {
        public string Name { get; set; } = "New Deck";
        public List<CardType> Cards { get; set; } = new();
    }
}
