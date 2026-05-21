using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Shared;

namespace GamePlay.Boards
{
    public class BoardCellsAnimator : IBoardCellsAnimator
    {
        public BoardCellsAnimator(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public async UniTask PlayTargetAnimation(IReadOnlyLifetime lifetime, Guid targetPlayer, IReadOnlyList<Position> positions)
        {
            if (positions == null || positions.Count == 0)
                return;

            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var tasks = new List<UniTask>();

            foreach (var position in positions)
            {
                var vector = position.ToVector();

                if (board.Cells.TryGetValue(vector, out var cell) == false)
                    continue;

                if (cell is CellView cellView)
                    tasks.Add(cellView.Visuals.PlayCellTarget(lifetime));
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }

        public async UniTask PlayActionAnimation(IReadOnlyLifetime lifetime, Guid targetPlayer, IReadOnlyList<Position> positions)
        {
            if (positions == null || positions.Count == 0)
                return;

            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var tasks = new List<UniTask>();

            foreach (var position in positions)
            {
                var vector = position.ToVector();

                if (board.Cells.TryGetValue(vector, out var cell) == false)
                    continue;

                if (cell is CellView cellView)
                    tasks.Add(cellView.Visuals.PlayCellAction(lifetime));
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }

        public UniTask OpenCells(IReadOnlyLifetime lifetime, Guid targetPlayer, IReadOnlyList<OpenedCell> cells)
        {
            if (cells == null)
                return UniTask.CompletedTask;

            var board = _gameContext.GetPlayer(targetPlayer).Board;

            foreach (var opened in cells)
            {
                var vector = opened.Position.ToVector();

                if (board.Cells.TryGetValue(vector, out var cell) == false)
                    continue;

                cell.EnsureFree().OnMinesUpdated(opened.MinesAround);
            }

            return UniTask.CompletedTask;
        }

        public void AddEffect(Guid effectId, CellEffectType type, Guid targetPlayer, Position position)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var vector = position.ToVector();

            if (board.Cells.TryGetValue(vector, out var cell) == false)
                return;

            if (cell is CellView cellView)
                cellView.Effects.AddEffect(effectId, type);
        }

        public void FlagCell(Guid targetPlayer, Position position)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var vector = position.ToVector();

            if (board.Cells.TryGetValue(vector, out var cell) == false)
                return;

            cell.EnsureTaken().OnFlagUpdated(true);
        }

        public void UnflagCell(Guid targetPlayer, Position position)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var vector = position.ToVector();

            if (board.Cells.TryGetValue(vector, out var cell) == false)
                return;

            cell.EnsureTaken().OnFlagUpdated(false);
        }
        public void EnsureTaken(Guid targetPlayer, Position position)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var vector = position.ToVector();

            if (board.Cells.TryGetValue(vector, out var cell) == false)
                return;

            cell.EnsureTaken();
        }

        public UniTask ExplodeCell(Guid targetPlayer, Position position, CellExplosionType type)
        {
            var board = _gameContext.GetPlayer(targetPlayer).Board;
            var vector = position.ToVector();

            if (board.Cells.TryGetValue(vector, out var cell) == false)
                return UniTask.CompletedTask;

            return cell.Explode(type);
        }
    }
}
