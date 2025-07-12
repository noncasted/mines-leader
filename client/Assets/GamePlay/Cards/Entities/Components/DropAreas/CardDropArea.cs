using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using GamePlay.Services;
using Global.Systems;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardDropResult
    {
        public bool IsSuccess { get; set; }
        public Vector2Int Position { get; set; }
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
            IReadOnlyList<IBoardCell> selected = null;
            var position = Vector2Int.zero;

            await _updater.RunUpdateAction(
                stateLifetime,
                () => selectionLifetime.IsTerminated == false,
                _ =>
                {
                    var board = GetSelectedBoard();

                    if (board == null || _context.TargetBoard != board)
                    {
                        DeselectAll();
                        selected = null;
                        return;
                    }

                    position = board.WorldToBoardPosition(_input.World);
                    var dropData = pattern.GetDropData(position);

                    if (selected != null)
                    {
                        foreach (var cell in selected)
                        {
                            if (dropData.Contains(cell) == false)
                                cell.Selection.Deselect();
                        }
                    }

                    foreach (var cell in dropData)
                    {
                        if (selected == null || selected.Contains(cell) == false)
                            cell.Selection.Select();
                    }

                    selected = dropData;
                }
            );

            DeselectAll();

            if (selected == null || selected.Count == 0)
                return new CardDropResult() { IsSuccess = false };

            return new CardDropResult()
            {
                IsSuccess = true,
                Position = position
            };

            IBoard GetSelectedBoard()
            {
                var input = _input.World;

                foreach (var player in _gameContext.All)
                {
                    if (player.Board.IsInside(input) == false)
                        continue;

                    return player.Board;
                }

                return null;
            }

            void DeselectAll()
            {
                if (selected == null)
                    return;

                foreach (var cell in selected)
                    cell.Selection.Deselect();
            }
        }
    }
}