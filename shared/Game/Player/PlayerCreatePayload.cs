using System;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerCreatePayload : IEntityPayload
    {
        public string Name { get; set; }
        public Guid Id { get; set; } 
        public CharacterType SelectedCharacter { get; set; }
    }
}