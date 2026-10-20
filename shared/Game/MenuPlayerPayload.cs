using System;
using MemoryPack;
using UnityEngine;

namespace Shared
{
    [MemoryPackable]
    public partial class MenuPlayerPayload : IEntityPayload
    {
        public Guid PlayerId { get; set; }
        public Vector2 Position { get; set; }
    }

    [MemoryPackUnion(0, typeof(MenuPlayerPayload))]
    public partial interface IEntityPayload
    {
    }
}
