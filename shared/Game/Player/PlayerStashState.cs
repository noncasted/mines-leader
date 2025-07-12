using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerStashState
    {
        public int Count { get; set; }
    }
}