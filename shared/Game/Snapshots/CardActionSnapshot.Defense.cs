#nullable enable annotations
using System;
using MemoryPack;

namespace Shared
{
    public partial class CardActionSnapshot
    {
        [MemoryPackable]
        public partial class Medic : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Purge : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Shield : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }
    }
}
