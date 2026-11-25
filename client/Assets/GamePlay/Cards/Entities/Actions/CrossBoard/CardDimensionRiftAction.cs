using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    /// <summary>
    /// Swaps a diamond area between both fields.
    /// </summary>
    public class CardDimensionRiftAction : ICardAction
    {
        public CardDimensionRiftAction(
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            ICardContext context,
            IGameContext gameContext,
            CardConfigOptions.DimensionRift config)
        {
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _context = context;
            _gameContext = gameContext;
            _config = config;
        }

        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly ICardContext _context;
        private readonly IGameContext _gameContext;
        private readonly CardConfigOptions.DimensionRift _config;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var pattern = new Pattern(_context.TargetBoard, _gameContext.Self.Board, _config.Size);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            var payload = new CardUsePayload.DimensionRift()
            {
                Type = _context.Type,
                Position = result.Position.ToPosition()
            };

            return new CardActionResult(result.IsSuccess, payload);
        }

        /// <summary>
        /// Превью подсвечивает ромб сразу на обеих досках: обмен идёт по одинаковым координатам.
        /// Ромб обрезается по доске цели, а доски обоих игроков одного размера, поэтому позиция
        /// без клетки у владельца — ошибка.
        /// </summary>
        public class Pattern : ICardDropPattern
        {
            public Pattern(IBoard targetBoard, IBoard ownerBoard, int size)
            {
                _targetBoard = targetBoard;
                _ownerBoard = ownerBoard;
                _shape = PatternShapes.Rhombus(size);
            }

            private readonly IBoard _targetBoard;
            private readonly IBoard _ownerBoard;
            private readonly IPattenShape _shape;
            private readonly List<IBoardCell> _targetCells = new();

            public void GetDropData(Vector2Int pointer, List<IBoardCell> result)
            {
                _targetCells.Clear();
                _shape.All(_targetBoard, pointer, _targetCells);

                foreach (var targetCell in _targetCells)
                {
                    if (_ownerBoard.Cells.TryGetValue(targetCell.BoardPosition, out var ownerCell) == false)
                    {
                        throw new InvalidOperationException(
                            $"Cell {targetCell.BoardPosition} of the target board is not on the board of the owner");
                    }

                    result.Add(targetCell);
                    result.Add(ownerCell);
                }
            }
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.DimensionRift>
        {
            public Snapshot(IBoardCellsAnimator animator)
            {
                _animator = animator;
            }

            private readonly IBoardCellsAnimator _animator;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.DimensionRift payload)
            {
                if (payload.TargetCells.Count > 0)
                {
                    await UniTask.WhenAll(
                        _animator.PlayTargetAnimation(lifetime, payload.TargetPlayer, payload.TargetCells),
                        _animator.PlayTargetAnimation(lifetime, payload.OwnerPlayer, payload.TargetCells));
                }

                await Apply(
                    lifetime,
                    payload.OwnerPlayer,
                    payload.OwnerTakenCells,
                    payload.OwnerFlaggedCells,
                    payload.OwnerUnflaggedCells,
                    payload.OwnerOpenedCells,
                    payload.OwnerUpdatedFreeCells);

                await Apply(
                    lifetime,
                    payload.TargetPlayer,
                    payload.TargetTakenCells,
                    payload.TargetFlaggedCells,
                    payload.TargetUnflaggedCells,
                    payload.TargetOpenedCells,
                    payload.TargetUpdatedFreeCells);
            }

            private async UniTask Apply(
                IReadOnlyLifetime lifetime,
                Guid player,
                IReadOnlyList<Position> taken,
                IReadOnlyList<Position> flagged,
                IReadOnlyList<Position> unflagged,
                IReadOnlyList<OpenedCell> opened,
                IReadOnlyList<OpenedCell> updatedFreeCells)
            {
                foreach (var position in taken)
                    _animator.EnsureTaken(player, position);

                foreach (var position in unflagged)
                    _animator.UnflagCell(player, position);

                foreach (var position in flagged)
                    _animator.FlagCell(player, position);

                await _animator.OpenCells(lifetime, player, opened);
                await _animator.OpenCells(lifetime, player, updatedFreeCells);
            }
        }
    }
}
