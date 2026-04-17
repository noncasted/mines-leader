using GamePlay.Boards;
using UnityEngine;

namespace Menu.Screens.Cards.Preview
{
    /// <summary>
    /// No-op <see cref="IBoardActions"/> used by the menu preview board. The preview
    /// is read-only: clicks do not produce network actions.
    /// </summary>
    public sealed class PreviewBoardActions : IBoardActions
    {
        public void Flag(Vector2Int position) {}
        public void Unflag(Vector2Int position) {}
        public void Open(Vector2Int position) {}
        public void OpenMultiple(Vector2Int position) {}
    }
}
