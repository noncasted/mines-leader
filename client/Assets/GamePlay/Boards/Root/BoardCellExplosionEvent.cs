using Common.Network;
using MemoryPack;
using UnityEngine;

namespace GamePlay.Boards
{
    [MemoryPackable]
    public partial class BoardCellExplosionEvent : IEventPayload
    {
        public BoardCellExplosionEvent(Vector2Int position)
        {
            Position = position;
        }

        public Vector2Int Position { get; }
    }
}