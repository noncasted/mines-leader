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
    }
}