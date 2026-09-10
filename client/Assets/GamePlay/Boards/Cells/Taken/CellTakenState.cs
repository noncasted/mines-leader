using Internal;

namespace GamePlay.Boards
{
    public class CellTakenState : ICellTakenState
    {
        public CellTakenState(IBoardCell cell, CellTakenView view, IBoardActions actions)
        {
            _cell = cell;
            _view = view;
            _actions = actions;
        }

        private readonly IBoardCell _cell;
        private readonly CellTakenView _view;
        private readonly IBoardActions _actions;

        private readonly ViewableProperty<bool> _isFlagged = new(false);

        public CellStatus Status => CellStatus.Taken;

        public IViewableProperty<bool> IsFlagged => _isFlagged;
        public CellTakenView View => _view;

        [Inject]
        public void Construct(IReadOnlyLifetime lifetime)
        {
            _view.Enable(lifetime, this);
        }

        public void Flag()
        {
            _actions.Flag(_cell.BoardPosition);
        }

        public void UnFlag()
        {
            _actions.Unflag(_cell.BoardPosition);
        }

        public void Open()
        {
            _actions.Open(_cell.BoardPosition);
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
