#nullable enable annotations
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial struct OpenedCell
    {
        public Position Position { get; set; }
        public int MinesAround { get; set; }
    }
}
