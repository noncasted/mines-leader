using UnityEngine;

namespace GamePlay.Boards
{
    public interface IBoardGenerator
    {
        void Generate(Vector2Int from);
    }
}