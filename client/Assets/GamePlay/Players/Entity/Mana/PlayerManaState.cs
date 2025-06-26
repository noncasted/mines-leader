using MemoryPack;

namespace GamePlay.Players
{ 
    [MemoryPackable]
    public partial class PlayerManaState
    {
        public int Current { get; set; }
        public int Max { get; set; }
    }
}