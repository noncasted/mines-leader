using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using GamePlay.Services;
using Global.Systems;
using Internal;

namespace GamePlay.Cards
{
    public interface ICardDropArea
    {
        UniTask<CardDropData> Show(
            IReadOnlyLifetime stateLifetime,
            IReadOnlyLifetime selectionLifetime,
            ICardDropPattern pattern);
    }
    
    public class CardDropArea : ICardDropArea
    {
        public CardDropArea(IUpdater updater, IGameInput input, IGameContext context)
        {
            _updater = updater;
            _input = input;
            _context = context;
        }

        private readonly IUpdater _updater;
        private readonly IGameInput _input;
        private readonly IGameContext _context;

        public async UniTask<CardDropData> Show(
            IReadOnlyLifetime stateLifetime,
            IReadOnlyLifetime selectionLifetime,
            ICardDropPattern pattern)
        {
            CardDropData previousData = null;

            await _updater.RunUpdateAction(
                stateLifetime,
                () => selectionLifetime.IsTerminated == false,
                _ =>
                {
                    var board = GetSelectedBoard();

                    if (board == null)
                    {
                        DeselectAll();
                        previousData = null;
                        return;
                    }

                    var boardPosition = board.WorldToBoardPosition(_input.World);
                    var dropData = pattern.GetDropData(board, boardPosition);

                    if (previousData != null)
                    {
                        foreach (var cell in previousData.Cells)
                        {
                            if (dropData.Cells.Contains(cell) == false)
                                cell.Selection.Deselect();
                        }
                    }

                    foreach (var cell in dropData.Cells)
                    {
                        if (previousData == null || previousData.Cells.Contains(cell) == false)
                            cell.Selection.Select();
                    }

                    previousData = dropData;
                });

            DeselectAll();

            return previousData;

            IBoard GetSelectedBoard()
            {
                var input = _input.World;

                foreach (var player in _context.All)
                {
                    if (player.Board.IsInside(input) == false)
                        continue;

                    return player.Board;
                }

                return null;
            }

            void DeselectAll()
            {
                if (previousData == null)
                    return;

                foreach (var cell in previousData.Cells)
                    cell.Selection.Deselect();
            }
        }
    }
}