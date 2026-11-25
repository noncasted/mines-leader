using System;
using GamePlay.Boards;
using Internal;

namespace GamePlay.Tests
{
    // The taken state of a FakeCell: the flag and the explosion, no board actions.
    public class FakeTakenState : ICellTakenState
    {
        public FakeTakenState(FakeCell cell)
        {
            _cell = cell;
        }

        private readonly FakeCell _cell;
        private readonly ViewableProperty<bool> _isFlagged = new(false);

        public CellStatus Status => CellStatus.Taken;
        public IViewableProperty<bool> IsFlagged => _isFlagged;

        public void Flag() => throw new NotSupportedException();
        public void UnFlag() => throw new NotSupportedException();
        public void Open() => throw new NotSupportedException();

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
