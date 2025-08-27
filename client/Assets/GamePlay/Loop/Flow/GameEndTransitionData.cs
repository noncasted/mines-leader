using System;

namespace GamePlay.Loop
{
    public class GameEndTransitionData
    {
        public bool ShouldRematch { get; set; }
        public Guid NewMatchId { get; set; }
    }
}