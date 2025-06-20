using Internal;

namespace GamePlay.Boards
{
    public interface ICellState
    {
        CellStatus Status { get; }
        
        INetworkCellState ToNetwork();
    }

    public interface ICellFreeState : ICellState
    {
        IViewableProperty<int> MinesAround { get; }
        
        void SetMinesAround(int minesAround);
    }

    public interface ICellTakenState : ICellState
    {
        IViewableProperty<bool> IsFlagged { get; }
        IViewableProperty<bool> HasMine { get; }
        
        void Flag();
        void UnFlag();
        void SetMine();
        bool Open();
    }
}