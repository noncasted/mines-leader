using System;
using Cysharp.Threading.Tasks;
using GamePlay.Boards.Effects;
using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class CellView : MonoBehaviour, IBoardCell
    {
        [SerializeField] private Vector2Int _boardPosition;
        [SerializeField] private CellPointerHandler _pointerHandler;

        [SerializeField] private CellFreeView _freeView;
        [SerializeField] private CellTakenView _takenView;

        [SerializeField] private Board _board;
        [SerializeField] private CellSelectionView _selection;

        [SerializeField] private CellAnimator _cellAnimator;
        [SerializeField] private CellVisuals _visuals;
        [SerializeField] private CellEffects _effects;

        private readonly ViewableProperty<ICellState> _state = new(null);
        private IBoardActions _actions;

        public Vector2Int BoardPosition => _boardPosition;
        public Vector2 WorldPosition => transform.position;

        public IViewableProperty<ICellState> State => _state;
        public ICellPointerHandler PointerHandler => _pointerHandler;
        public IBoard Source => _board;
        public ICellSelectionView Selection => _selection;
        public CellVisuals Visuals => _visuals;
        public CellEffects Effects => _effects;

        public bool IsAnimating => AnimatorKind != null;

        public string AnimatorKind
        {
            get
            {
                if (_cellAnimator != null && _cellAnimator.IsPlaying)
                    return _cellAnimator.PlayingKind;

                if (_takenView != null && _takenView.FlagAnimator != null && _takenView.FlagAnimator.IsPlaying)
                    return "flag";

                return null;
            }
        }

        public CellInspect Inspect()
        {
            var inspect = new CellInspect
            {
                X = _boardPosition.x,
                Y = _boardPosition.y,
                Exists = true,
                Active = isActiveAndEnabled,
                Flagged = false,
                MinesAround = null,
                Effects = new System.Collections.Generic.List<string>(),
                Animator = AnimatorKind
            };

            if (_state.Value is ICellFreeState free)
            {
                inspect.State = "free";
                inspect.MinesAround = free.MinesAround.Value;
                return inspect;
            }

            inspect.State = "taken";
            if (_state.Value is ICellTakenState taken)
                inspect.Flagged = taken.IsFlagged.Value;

            return inspect;
        }

        public void Construct(Vector2Int position, Board board)
        {
            _boardPosition = position;
            _board = board;
        }

        public void Setup(IUpdater updater, IBoardActions actions)
        {
            _actions = actions;
            _cellAnimator.Construct(updater);
            _visuals.Construct(updater);
            _takenView.FlagAnimator.Construct(updater);
            var taken = new CellTakenState(this, _takenView, _actions);
            _state.Set(taken);
            taken.Construct(_state.ValueLifetime);
        }

        public ICellTakenState EnsureTaken()
        {
            if (_state.Value is not CellTakenState)
            {
                Effects.Clear();

                var taken = new CellTakenState(this, _takenView, _actions);
                _state.Set(taken);
                taken.Construct(_state.ValueLifetime);
            }

            return (CellTakenState)_state.Value;
        }

        public ICellFreeState EnsureFree()
        {
            if (_state.Value is not CellFreeState)
            {
                Effects.Clear();

                _cellAnimator.PlayOpen(this.GetObjectLifetime()).Forget();
                var free = new CellFreeState(_boardPosition, _freeView);
                _state.Set(free);
                free.Construct(_state.ValueLifetime);
            }

            return (CellFreeState)_state.Value;
        }

        public UniTask Explode(CellExplosionType type)
        {
            if (_state.Value is not CellTakenState state)
                throw new Exception("Cell is not taken, cannot explode.");

            state.View.OnExplosion();

            EnsureFree();

            return _cellAnimator.PlayExplosion(this.GetObjectLifetime(), type);
        }

        public void ResetState()
        {
            _state.Set(null);
        }

        public override string ToString()
        {
            return name;
        }
    }
}
