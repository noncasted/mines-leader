using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [SharedGrainState(Table = "configs", State = "loot_progression_config", Key = GrainKeyType.String,
        Lookup = "LootProgressionConfig")]
    public partial class LootProgressionOptions : INetworkContext
    {
        public List<int> Thresholds { get; set; } = new();

        public static LootProgressionOptions CreateDefault()
        {
            return new LootProgressionOptions
            {
                Thresholds = new List<int> { 200, 500, 1000, 2000, 5000 }
            };
        }
    }
}