using System.Collections.Generic;
using MemoryPack;
using UnityEngine;

namespace GamePlay.Boards
{
    [MemoryPackable]
    public partial class NetworkBoardCellsState
    {
        public Dictionary<Vector2Int, INetworkCellState> Cells { get; set; } 
    }

    [MemoryPackable]
    public partial class NetworkCellFreeState : INetworkCellState
    {
        public CellStatus Status => CellStatus.Free;
    }

    [MemoryPackable]
    public partial class NetworkCellTakenState : INetworkCellState
    {
        public CellStatus Status => CellStatus.Taken;
        public bool IsFlagged { get; set; }
        public bool HasMine { get; set; }
    }

    [MemoryPackable]
    [MemoryPackUnion(0, typeof(NetworkCellFreeState))]
    [MemoryPackUnion(1, typeof(NetworkCellTakenState))]
    public partial interface INetworkCellState
    {
        public CellStatus Status { get; }
    }
}