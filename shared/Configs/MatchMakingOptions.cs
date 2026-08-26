using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [SharedGrainState(Table = "configs", State = "matchmaking_config", Key = GrainKeyType.String,
        Lookup = "MatchMakingConfig")]
    public partial class MatchMakingOptions : INetworkContext
    {
        public List<GameMatchType> Available { get; set; } = new();

        public static MatchMakingOptions CreateDefault()
        {
            return new MatchMakingOptions
            {
                Available = new List<GameMatchType>
                {
                    GameMatchType.TimeLimited,
                    GameMatchType.LastManStanding
                }
            };
        }
    }
}
