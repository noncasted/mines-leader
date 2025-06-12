using System.Collections.Generic;
using Global.GameServices;
using MemoryPack;
using Shared;

namespace GamePlay.Cards
{
    [MemoryPackable]
    public partial class DeckState
    {
        public List<CardType> Queue { get; set; }
        
        public CardType Pick()
        {
            var card = Queue[0];
            Queue.RemoveAt(0);
            return card;
        }
    }
}