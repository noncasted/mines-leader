using Global.Backend;
using Internal;
using Shared;

namespace GamePlay.Boards
{
    public class CellTakenState : ICellTakenState
    {
        public CellTakenState(IBoardCell cell, CellTakenView view, INetworkConnection connection)
        {
            _cell = cell;
            _view = view;
            _connection = connection;
        }

        private readonly IBoardCell _cell;
        private readonly CellTakenView _view;
        private readonly INetworkConnection _connection;

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
            _connection.Request(new SharedGameAction.SetFlag()
            {
                Position = _cell.BoardPosition.ToPosition()
            });
        }

        public void UnFlag()
        {
            _connection.Request(new SharedGameAction.RemoveFlag()
            {
                Position = _cell.BoardPosition.ToPosition()
            });
        }

        public void Open()
        {
            _connection.Request(new SharedGameAction.Open()
            {
                Position = _cell.BoardPosition.ToPosition()
            });
        }

        public void Explode(CellExplosionType type)
        {
            _cell.Explode(type);
        }

        public void OnFlagUpdated(bool isFlagged)
        {
            _isFlagged.Set(isFlagged);
        }
    }
}