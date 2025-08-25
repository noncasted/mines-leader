using System;
using MemoryPack;
using UnityEngine;

namespace Shared
{
    [MemoryPackable]
    public partial class CardCreatePayload : IEntityPayload
    {
        public CardType Type { get; set; }
        public Guid OwnerId { get; set; }
        public Vector2 SpawnPoint { get; set; }
    }
}