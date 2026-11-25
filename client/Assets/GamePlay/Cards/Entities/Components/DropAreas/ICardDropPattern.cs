using System.Collections.Generic;
using GamePlay.Boards;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardDropPattern
    {
        // Adds the cells the card takes with the pointer at pointer to result (an empty buffer of the caller).
        void GetDropData(Vector2Int pointer, List<IBoardCell> result);
    }
}