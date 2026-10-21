using System;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class CardCreatePayload : IEntityPayload
    {
        public CardType Type { get; set; }
        public Guid OwnerId { get; set; }
    }

    [MemoryPackUnion(1, typeof(CardCreatePayload))]
    public partial class EntityPayloadUnion
    {
    }
}
