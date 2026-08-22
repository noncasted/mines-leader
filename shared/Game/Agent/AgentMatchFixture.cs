using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class AgentMatchFixture
    {
        public string SelfBoardLayout { get; set; } = string.Empty;
        public List<CardType> SelfHand { get; set; } = new List<CardType>();
        public List<CardType> SelfDeck { get; set; } = new List<CardType>();
        public List<CardType> BotDeck { get; set; } = new List<CardType>();
        public BotProfile? BotProfile { get; set; }
        public bool HumanGoesFirst { get; set; } = true;
        public int? Mana { get; set; }
        public int? Moves { get; set; }
    }
}
