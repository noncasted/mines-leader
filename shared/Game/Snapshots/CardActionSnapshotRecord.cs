using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(CardActionSnapshot.ZipZap))]
    [MemoryPackUnion(1, typeof(CardActionSnapshot.Bloodhound))]
    [MemoryPackUnion(2, typeof(CardActionSnapshot.ErosionDozer))]
    [MemoryPackUnion(3, typeof(CardActionSnapshot.Gravedigger))]
    [MemoryPackUnion(4, typeof(CardActionSnapshot.Trebuchet))]
    [MemoryPackUnion(5, typeof(CardActionSnapshot.TrebuchetAimer))]
    [MemoryPackUnion(6, typeof(CardActionSnapshot.OpponentBomb))]
    [MemoryPackUnion(7, typeof(CardActionSnapshot.OpponentFlagErase))]
    [MemoryPackUnion(8, typeof(CardActionSnapshot.OpponentFlagReshuffle))]
    [MemoryPackUnion(9, typeof(CardActionSnapshot.Smoke))]
    [MemoryPackUnion(10, typeof(CardActionSnapshot.Medic))]
    [MemoryPackUnion(11, typeof(CardActionSnapshot.MinefieldScout))]
    [MemoryPackUnion(13, typeof(CardActionSnapshot.Siphon))]
    [MemoryPackUnion(16, typeof(CardActionSnapshot.ChainReaction))]
    [MemoryPackUnion(17, typeof(CardActionSnapshot.Overclock))]
    [MemoryPackUnion(18, typeof(CardActionSnapshot.FogOfWar))]
    [MemoryPackUnion(19, typeof(CardActionSnapshot.Scavenger))]
    [MemoryPackUnion(20, typeof(CardActionSnapshot.HandScramble))]
    [MemoryPackUnion(21, typeof(CardActionSnapshot.Lockdown))]
    [MemoryPackUnion(23, typeof(CardActionSnapshot.Sonar))]
    [MemoryPackUnion(24, typeof(CardActionSnapshot.Purge))]
    public partial interface ICardActionData
    {
        Guid TargetPlayer { get; set; }
    }

    public partial class CardActionSnapshot
    {
        [MemoryPackable]
        public partial class ZipZap : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> Targets { get; set; }
        }

        [MemoryPackable]
        public partial class Bloodhound : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class ErosionDozer : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Gravedigger : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Trebuchet : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class TrebuchetAimer : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class OpponentBomb : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class OpponentFlagErase : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class OpponentFlagReshuffle : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Smoke : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Medic : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class MinefieldScout : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> RevealedCells { get; set; }
        }

        [MemoryPackable]
        public partial class Siphon : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class ChainReaction : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> SpawnedMines { get; set; }
        }

        [MemoryPackable]
        public partial class Overclock : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class FogOfWar : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Scavenger : ICardActionData
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
        public partial class Sonar : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> FlaggedCells { get; set; }
        }

        [MemoryPackable]
        public partial class Purge : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }
    }
}