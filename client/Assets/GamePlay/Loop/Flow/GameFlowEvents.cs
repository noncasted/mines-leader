using System;
using Common.Network;
using MemoryPack;

namespace GamePlay.Loop
{
    public partial class GameFlowEvents
    {
        [MemoryPackable]
        public partial class Lose : IEventPayload
        {
            public Lose(Guid playerId)
            {
                PlayerId = playerId;
            }

            public Guid PlayerId { get; }
        }
    }
}