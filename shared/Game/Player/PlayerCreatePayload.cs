using System;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerCreatePayload : IEntityPayload
    {
        public string Name { get; init; }
        public Guid Id { get; init; } 
        public CharacterType SelectedCharacter { get; init; }
    }
}