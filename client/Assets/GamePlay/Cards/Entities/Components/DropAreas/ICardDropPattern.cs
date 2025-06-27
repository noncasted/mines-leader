using System.Collections.Generic;
using GamePlay.Boards;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardDropPattern
    {
        IReadOnlyList<IBoardCell> GetDropData(Vector2Int pointer);
    }
}