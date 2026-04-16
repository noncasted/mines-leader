using System;
using Cysharp.Threading.Tasks;
using GamePlay.Boards.Effects;
using Global.Systems;
using Internal;
using Network;
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
        private INetworkConnection _connection;

        public Vector2Int BoardPosition => _boardPosition;
        public Vector2 WorldPosition => transform.position;

        public IViewableProperty<ICellState> State => _state;
        public ICellPointerHandler PointerHandler => _pointerHandler;
        public IBoard Source => _board;
        public ICellSelectionView Selection => _selection;
        public CellVisuals Visuals => _visuals;
        public CellEffects Effects => _effects;

        public void ConstructFromBuild(Vector2Int position, Board board)
        {
            _boardPosition = position;
            _board = board;
        }

        public void Setup(IUpdater updater, INetworkConnection connection)
        {
            _connection = connection;
            _cellAnimator.Construct(updater);
            _visuals.Construct(updater);
            _takenView.FlagAnimator.Construct(updater);
            var taken = new CellTakenState(this, _takenView, _connection);
            _state.Set(taken);
            taken.Construct(_state.ValueLifetime);
        }

        public ICellTakenState EnsureTaken()
        {
            if (_state.Value is not CellTakenState)
            {
                Effects.Clear();

                if (_visuals.HasPendingTarget)
                    _visuals.PlayCellAction(this.GetObjectLifetime()).Forget();

                var taken = new CellTakenState(this, _takenView, _connection);
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

                if (_visuals.HasPendingTarget)
                    _visuals.PlayCellAction(this.GetObjectLifetime()).Forget();

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

        public override string ToString()
        {
            return name;
        }
    }
}