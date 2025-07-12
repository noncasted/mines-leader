using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
    public interface IBoardCell
    {
        Vector2Int BoardPosition { get; }
        Vector2 WorldPosition { get; }
        
        IViewableProperty<ICellState> State { get; }
        ICellPointerHandler PointerHandler { get; }
        IBoard Source { get; }
        ICellSelectionView Selection { get; }
        
        ICellTakenState EnsureTaken();
        ICellFreeState EnsureFree();
        UniTask Explode(CellExplosionType type);
    }
    
    public enum CellExplosionType
    {
        Mine,
        ZipZap
    }

    public static class BoardCellExtensions
    {
        public static bool IsTaken(this IBoardCell cell)
        {
            return cell.State.Value.Status == CellStatus.Taken;
        }
        
        public static bool IsFree(this IBoardCell cell)
        {
            return cell.State.Value.Status == CellStatus.Free;
        }
        
        public static bool HasFlag(this IBoardCell cell)
        {
            if (cell.State.Value.Status == CellStatus.Free)
                return false;
            
            var state = cell.EnsureTaken();
            return state.IsFlagged.Value;
        }
    }
}