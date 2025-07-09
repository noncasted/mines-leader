using System.Collections.Generic;
using MemoryPack;
using UnityEngine;

namespace GamePlay.Cards
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(CardUseEvents.Bloodhound))]
    [MemoryPackUnion(1, typeof(CardUseEvents.ErosionDozer))]
    [MemoryPackUnion(2, typeof(CardUseEvents.Gravedigger))]
    [MemoryPackUnion(3, typeof(CardUseEvents.Trebuchet))]
    [MemoryPackUnion(4, typeof(CardUseEvents.TrebuchetAimer))]
    [MemoryPackUnion(5, typeof(CardUseEvents.ZipZap))]
    public partial interface ICardUseEvent
    {
    }

    [MemoryPackable]
    public partial class CardUseEvents
    {
        [MemoryPackable]
        public partial class Bloodhound : ICardUseEvent
        {
        }

        [MemoryPackable]
        public partial class ErosionDozer : ICardUseEvent
        {
        }

        [MemoryPackable]
        public partial class Gravedigger : ICardUseEvent
        {
        }

        [MemoryPackable]
        public partial class Trebuchet : ICardUseEvent
        {
        }

        [MemoryPackable]
        public partial class TrebuchetAimer : ICardUseEvent
        {
        }

        [MemoryPackable]
        public partial class ZipZap : ICardUseEvent
        {
            public ZipZap(List<Vector2Int> targets)
            {
                Targets = targets;
            }

            public List<Vector2Int> Targets { get; }
        }
    }
}