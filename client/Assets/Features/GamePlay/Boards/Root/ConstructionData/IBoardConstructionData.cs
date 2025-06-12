using UnityEngine;

namespace GamePlay.Boards
{
    public interface IBoardConstructionData
    {
        Vector2Int Size { get; }
        float CellSize { get; }
    }
}