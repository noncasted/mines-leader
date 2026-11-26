#nullable enable annotations
using System;
using MemoryPack;

namespace Shared
{
    public partial class CardActionSnapshot
    {
        [MemoryPackable]
        public partial class Siphon : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class HandScramble : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Lockdown : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Embargo : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class CardThief : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public CardType StolenCard { get; set; }
        }

        [MemoryPackable]
        public partial class SabotageDeck : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Dud : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class SoulLink : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }
    }
}
