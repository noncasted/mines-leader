using System;
using Internal;

namespace GamePlay.Boards
{
    public class CellTakenState : ICellTakenState
    {
        public CellTakenState(IBoardCell cell, CellTakenView view)
        {
            _cell = cell;
            _view = view;
        }

        private readonly IBoardCell _cell;
        private readonly CellTakenView _view;

        private readonly ViewableProperty<bool> _isFlagged = new(false);

        public CellStatus Status => CellStatus.Taken;

        public IViewableProperty<bool> IsFlagged => _isFlagged;
        public CellTakenView View => _view;

        public void Construct(IReadOnlyLifetime lifetime)
        {
            _view.Enable(lifetime, this);
        }

        public void Flag()
        {
            _isFlagged.Set(true);
        }

        public void UnFlag()
        {
            _isFlagged.Set(false);
        }

        public void Open()
        {
        }

        public void OnFlagUpdated(bool isFlagged)
        {
            throw new NotImplementedException();
        }
    }
}