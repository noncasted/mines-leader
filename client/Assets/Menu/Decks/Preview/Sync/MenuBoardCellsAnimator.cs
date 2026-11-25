using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;

namespace Menu.Decks
{
    public class MenuBoardCellsAnimator : IBoardCellsAnimator
    {
        public MenuBoardCellsAnimator(IMenuBoard menuBoard)
        {
            _menuBoard = menuBoard;
        }

        private readonly IMenuBoard _menuBoard;

        public async UniTask PlayTargetAnimation(IReadOnlyLifetime lifetime, Guid targetPlayer, IReadOnlyList<Position> positions)
        {
            var board = _menuBoard.Board;
            var tasks = new List<UniTask>();

            foreach (var position in positions)
            {
                var cell = board.GetSyncedCell(targetPlayer, position);

                tasks.Add(cell.Visuals.PlaySelection(lifetime));
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }

        public async UniTask PlayActionAnimation(IReadOnlyLifetime lifetime, Guid targetPlayer, IReadOnlyList<Position> positions)
        {
            var board = _menuBoard.Board;
            var tasks = new List<UniTask>();

            foreach (var position in positions)
            {
                var cell = board.GetSyncedCell(targetPlayer, position);

                tasks.Add(cell.Visuals.PlayCellAction(lifetime));
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }

        public UniTask OpenCells(IReadOnlyLifetime lifetime, Guid targetPlayer, IReadOnlyList<OpenedCell> cells)
        {
            var board = _menuBoard.Board;

            foreach (var opened in cells)
            {
                var cell = board.GetSyncedCell(targetPlayer, opened.Position);

                cell.EnsureFree().OnMinesUpdated(opened.MinesAround);
            }

            return UniTask.CompletedTask;
        }

        public void AddEffect(Guid effectId, CellEffectType type, Guid targetPlayer, Position position)
        {
            var cell = _menuBoard.Board.GetSyncedCell(targetPlayer, position);

            cell.Effects.Add(effectId, type);
        }

        public void FlagCell(Guid targetPlayer, Position position)
        {
            var cell = _menuBoard.Board.GetSyncedCell(targetPlayer, position);

            cell.EnsureTaken().OnFlagUpdated(true);
        }

        public void UnflagCell(Guid targetPlayer, Position position)
        {
            var cell = _menuBoard.Board.GetSyncedCell(targetPlayer, position);

            cell.EnsureTaken().OnFlagUpdated(false);
        }

        public void EnsureTaken(Guid targetPlayer, Position position)
        {
            var cell = _menuBoard.Board.GetSyncedCell(targetPlayer, position);

            cell.EnsureTaken();
        }

        public UniTask ExplodeCell(Guid targetPlayer, Position position, CellExplosionType type)
        {
            var cell = _menuBoard.Board.GetSyncedCell(targetPlayer, position);

            return cell.Explode(type);
        }
    }
}
