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

        [SerializeField] private CellFreeView _freeView;
        [SerializeField] private CellTakenView _takenView;

        [SerializeField] private Board _board;
        [SerializeField] private CellSelectionView _selection;

        [SerializeField] private CellAnimator _cellAnimator;
        [SerializeField] private CellVisuals _visuals;
        [SerializeField] private CellEffects _effects;

        private readonly ViewableProperty<ICellState> _state = new(null);
        private readonly CellEffectsState _effectsState = new();
        private IBoardActions _actions;

        public Vector2Int BoardPosition => _boardPosition;
        public Vector2 WorldPosition => transform.position;

        public IViewableProperty<ICellState> State => _state;
        public IBoard Source => _board;
        public ICellSelectionView Selection => _selection;
        public ICellVisuals Visuals => _visuals;
        public ICellEffectsState Effects => _effectsState;

        public string PlayingAnimation
        {
            get
            {
                if (_cellAnimator.IsPlaying)
                    return _cellAnimator.PlayingKind;

                if (_takenView.FlagAnimator.IsPlaying)
                    return "flag";

                return null;
            }
        }

        [Inject]
        public void Construct(Vector2Int position, Board board)
        {
            _boardPosition = position;
            _board = board;
        }

        // The lifetime of the board: the view of the effects follows their state while it lives.
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _effects.View(lifetime, _effectsState);
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
                _effectsState.Clear();

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
                _effectsState.Clear();

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
            _effectsState.Clear();
            _state.Set(null);
        }

        public override string ToString()
        {
            return name;
        }
    }
}
