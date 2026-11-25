using System;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Boards.Effects;
using Internal;
using UnityEngine;

namespace GamePlay.Tests
{
    // A cell without a MonoBehaviour. Its states follow CellView: a new cell is taken, a change of the status creates a
    // new state and clears the effects, Explode turns a taken cell free and remembers the kind of the explosion.
    public class FakeCell : IBoardCell
    {
        public FakeCell(FakeBoard source, Vector2Int boardPosition)
        {
            _board = source;
            BoardPosition = boardPosition;
            _state.Set(new FakeTakenState(this));
        }

        private readonly ViewableProperty<ICellState> _state = new(null);
        private readonly CellEffectsState _effects = new();
        private readonly FakeBoard _board;

        public Vector2Int BoardPosition { get; }
        public Vector2 WorldPosition =>
            _board.LocalToWorld(_board.ConstructionDataData.GetLocalPosition(BoardPosition));

        public IBoard Source => _board;

        public IViewableProperty<ICellState> State => _state;
        public ICellEffectsState Effects => _effects;
        public CellExplosionType? Exploded { get; private set; }

        public ICellSelectionView Selection => throw new NotSupportedException();
        public ICellVisuals Visuals => throw new NotSupportedException();
        public string PlayingAnimation => null;

        public ICellTakenState EnsureTaken()
        {
            if (_state.Value is not FakeTakenState)
            {
                _effects.Clear();
                _state.Set(new FakeTakenState(this));
            }

            return (FakeTakenState)_state.Value;
        }

        public ICellFreeState EnsureFree()
        {
            if (_state.Value is not FakeFreeState)
            {
                _effects.Clear();
                _state.Set(new FakeFreeState());
            }

            return (FakeFreeState)_state.Value;
        }

        public UniTask Explode(CellExplosionType type)
        {
            if (_state.Value is not FakeTakenState)
                throw new InvalidOperationException("Cell is not taken, cannot explode.");

            Exploded = type;
            EnsureFree();

            return UniTask.CompletedTask;
        }

        public override string ToString()
        {
            return $"Cell_{BoardPosition.x}_{BoardPosition.y}";
        }
    }
}
