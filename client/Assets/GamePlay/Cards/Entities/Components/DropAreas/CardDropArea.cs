using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public readonly struct CardDropResult
    {
        public CardDropResult(bool isSuccess, Vector2Int position)
        {
            IsSuccess = isSuccess;
            Position = position;
        }

        public static CardDropResult Failed => new(false, BoardExtensions.Outside);

        public bool IsSuccess { get; }
        public Vector2Int Position { get; }
    }

    public interface ICardDropArea
    {
        UniTask<CardDropResult> Show(
            IReadOnlyLifetime stateLifetime,
            IReadOnlyLifetime selectionLifetime,
            ICardDropPattern pattern);
    }

    public class CardDropArea : ICardDropArea
    {
        public CardDropArea(
            IUpdater updater,
            IGameInput input,
            IGameContext gameContext,
            ICardContext context)
        {
            _updater = updater;
            _input = input;
            _gameContext = gameContext;
            _context = context;
        }

        private readonly IUpdater _updater;
        private readonly IGameInput _input;
        private readonly IGameContext _gameContext;
        private readonly ICardContext _context;

        public async UniTask<CardDropResult> Show(
            IReadOnlyLifetime stateLifetime,
            IReadOnlyLifetime selectionLifetime,
            ICardDropPattern pattern)
        {
            // The drop data changes only with the cell under the pointer: it is computed again when the pointer moves
            // to another cell of the target board, not every frame. Buffers are of this aim, reused between cells.
            var dropData = new List<IBoardCell>();
            var selected = new HashSet<IBoardCell>();
            var next = new HashSet<IBoardCell>();
            IBoard aimedBoard = null;
            var position = BoardExtensions.Outside;

            // The end of the state lifetime (the turn is over, the card is taken over or torn down) cancels the
            // selection: nothing is dropped then.
            var isCanceled = await _updater.RunUpdateAction(
                stateLifetime,
                () => selectionLifetime.IsTerminated == false,
                _ => {
                    if (_gameContext.IsPaused == true)
                        return;

                    var board = GetSelectedBoard();

                    if (board == null || _context.TargetBoard != board)
                    {
                        DeselectAll();
                        aimedBoard = null;
                        return;
                    }

                    var pointer = board.WorldToBoardPosition(_input.World);

                    if (board == aimedBoard && pointer == position)
                        return;

                    aimedBoard = board;
                    position = pointer;
                    UpdateSelection();
                }).SuppressCancellationThrow();

            DeselectAll();

            if (isCanceled == true || aimedBoard == null)
                return CardDropResult.Failed;

            // The cells could change while the pointer stayed on one cell: the drop is judged by the data of now.
            dropData.Clear();
            pattern.GetDropData(position, dropData);

            if (dropData.Count == 0)
                return CardDropResult.Failed;

            return new CardDropResult(true, position);

            void UpdateSelection()
            {
                dropData.Clear();
                pattern.GetDropData(position, dropData);

                next.Clear();
                next.UnionWith(dropData);

                foreach (var cell in selected)
                {
                    if (next.Contains(cell) == false)
                        cell.Selection.Deselect();
                }

                foreach (var cell in next)
                {
                    if (selected.Contains(cell) == false)
                        cell.Selection.Select();
                }

                (selected, next) = (next, selected);
            }

            IBoard GetSelectedBoard()
            {
                var input = _input.World;
                var players = _gameContext.All;

                for (var i = 0; i < players.Count; i++)
                {
                    var board = players[i].Board;

                    if (board.IsInside(input) == true)
                        return board;
                }

                return null;
            }

            void DeselectAll()
            {
                foreach (var cell in selected)
                    cell.Selection.Deselect();

                selected.Clear();
            }
        }
    }
}
