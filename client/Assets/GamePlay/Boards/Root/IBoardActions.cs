using UnityEngine;

namespace GamePlay.Boards
{
    public interface IBoardActions
    {
        void Flag(Vector2Int position);
        void Unflag(Vector2Int position);
        void Open(Vector2Int position);
        void OpenMultiple(Vector2Int position);
    }
}
