using GamePlay.Boards;
using Internal;

namespace GamePlay.Tests
{
    // The free state of a FakeCell: the number of mines around.
    public class FakeFreeState : ICellFreeState
    {
        private readonly ViewableProperty<int> _minesAround = new();

        public CellStatus Status => CellStatus.Free;
        public IViewableProperty<int> MinesAround => _minesAround;

        public void OnMinesUpdated(int minesAround)
        {
            _minesAround.Set(minesAround);
        }
    }
}
