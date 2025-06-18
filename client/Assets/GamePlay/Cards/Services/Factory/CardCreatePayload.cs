using System;
using Common.Network;
using MemoryPack;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    [MemoryPackable]
    public partial class CardCreatePayload : IEntityPayload
    {
        public CardType Type { get; set; }
        public Guid OwnerId { get; set; }
        public Vector2 SpawnPoint { get; set; }
    }
}