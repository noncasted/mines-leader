using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
    public class CellFreeState : ICellFreeState
    {
        public CellFreeState(
            Vector2Int position,
            CellFreeView view)
        {
            _position = position;
            _view = view;
        }

        private readonly Vector2Int _position;
        private readonly CellFreeView _view;

        private readonly ViewableProperty<int> _minesAround = new();

        public CellStatus Status => CellStatus.Free;
        public IViewableProperty<int> MinesAround => _minesAround;

        public void Construct(IReadOnlyLifetime lifetime)
        {
            _view.Enable(lifetime, this);
        }

        public void OnMinesUpdated(int minesAround)
        {
            _minesAround.Set(minesAround);
        }
    }
}