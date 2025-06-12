using GamePlay.Boards.Taken;
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

        private readonly ViewableProperty<ICellState> _state = new(null);

        public Vector2Int BoardPosition => _boardPosition;
        public Vector2 WorldPosition => transform.position;

        public IViewableProperty<ICellState> State => _state;
        public ICellPointerHandler PointerHandler => _pointerHandler;
        public IBoard Source => _board;
        public ICellSelectionView Selection => _selection;

        public void ConstructFromBuild(Vector2Int position, Board board)
        {
            _boardPosition = position;
            _board = board;
        }

        public void Setup()
        {
            var taken = new CellTakenState(this, _takenView);
            _state.Set(taken);
            taken.Construct(_state.ValueLifetime);
        }

        public ICellTakenState EnsureTaken()
        {
            if (_state.Value is not CellTakenState)
            {
                var taken = new CellTakenState(this, _takenView);
                _state.Set(taken);
                taken.Construct(_state.ValueLifetime);
            }

            return (CellTakenState)_state.Value;
        }

        public ICellFreeState EnsureFree()
        {
            if (_state.Value is not CellFreeState)
            {
                var free = new CellFreeState(_boardPosition, _freeView);
                _state.Set(free);
                free.Construct(_state.ValueLifetime);
            }

            return (CellFreeState)_state.Value;
        }

        public void UpdateState(INetworkCellState networkState)
        {
            switch (networkState)
            {
                case NetworkCellFreeState free:
                    ((CellFreeState)EnsureFree()).OnUpdate(free);
                    break;

                case NetworkCellTakenState taken:
                    ((CellTakenState)EnsureTaken()).OnUpdate(taken);
                    break;
            }
        }

        public override string ToString()
        {
            return name;
        }
    }
}