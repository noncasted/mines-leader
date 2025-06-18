using System.Collections.Generic;
using GamePlay.Boards;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardDropPattern
    {
        CardDropData GetDropData(IBoard board, Vector2Int pointer);
    }

    public class CardDropData
    {
        public CardDropData(
            IReadOnlyList<IBoardCell> cells,
            IBoard board)
        {
            Cells = cells;
            Board = board;
        }

        public IReadOnlyList<IBoardCell> Cells { get; }
        public IBoard Board { get; }
        
        public static CardDropData Empty(IBoard board) => new(new List<IBoardCell>(), board);
    }
}