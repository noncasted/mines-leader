using System;
using Common.Network;
using MemoryPack;
using Meta;
using Shared;

namespace GamePlay.Players
{
    [MemoryPackable]
    public partial class GamePlayerCreatePayload : IEntityPayload
    {
        public string Name { get; set; }
        public Guid Id { get; set; } 
        public CharacterType SelectedCharacter { get; set; }
    }
}