using System.Collections.Generic;
using Global.GameServices;
using MemoryPack;
using Shared;

namespace GamePlay.Cards
{
    [MemoryPackable]
    public partial class StashState
    {
        public List<CardType> Stack { get; set; }
        
        public CardType Pick()
        {
            var card = Stack[^1];
            Stack.RemoveAt(Stack.Count - 1);
            return card;
        }
    }
}