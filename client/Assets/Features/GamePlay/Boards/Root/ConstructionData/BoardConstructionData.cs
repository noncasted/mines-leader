using System;
using UnityEngine;

namespace GamePlay.Boards
{
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