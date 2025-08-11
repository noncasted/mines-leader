using Internal;

namespace GamePlay.Boards
{
    public interface ICellState
    {
        CellStatus Status { get; }
    }

    public interface ICellFreeState : ICellState
    {
        IViewableProperty<int> MinesAround { get; }
        
        void OnMinesUpdated(int minesAround);
    }

    public interface ICellTakenState : ICellState
    {
        IViewableProperty<bool> IsFlagged { get; }
        
        void Flag();
        void UnFlag();
        void Open();

        void OnFlagUpdated(bool isFlagged);
    }
}