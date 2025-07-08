using Internal;

namespace GamePlay.Boards
{
    public class CellTakenState : ICellTakenState
    {
        public CellTakenState(
            IBoardCell cell,
            CellTakenView view)
        {
            _cell = cell;
            _view = view;
        }

        private readonly IBoardCell _cell;
        private readonly CellTakenView _view;

        private readonly ViewableProperty<bool> _isFlagged = new(false);
        private readonly ViewableProperty<bool> _hasMine = new(false);

        public CellStatus Status => CellStatus.Taken;

        public IViewableProperty<bool> IsFlagged => _isFlagged;
        public IViewableProperty<bool> HasMine => _hasMine;
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

        public void SetMine()
        {
            _hasMine.Set(true);
        }

        public bool Open()
        {
            if (_hasMine.Value == false)
            {
                _cell.EnsureFree();
                return false;
            }

            _cell.Explode(CellExplosionType.Mine);
            _view.OnExplosion();
            return true;
        }

        public void OnUpdate(NetworkCellTakenState payload)
        {
            _isFlagged.Set(payload.IsFlagged);
            _hasMine.Set(payload.HasMine);
        }

        public INetworkCellState ToNetwork()
        {
            return new NetworkCellTakenState()
            {
                IsFlagged = _isFlagged.Value,
                HasMine = _hasMine.Value
            };
        }
    }
}