using System;
using UnityEngine;

namespace GamePlay.Boards
{
    public interface IBoardConstructionData
    {
        Vector2Int Size { get; }
        float CellSize { get; }
    }
    
    [Serializable]
    public class BoardConstructionData : IBoardConstructionData
    {
        public BoardConstructionData(Vector2Int size, float cellSize)
        {
            Size = size;
            CellSize = cellSize;
        }

        [field: SerializeField] public Vector2Int Size { get; private set; }
        [field: SerializeField] public float CellSize { get; private set; }
    }
}