using System.Collections.Generic;
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
        UniTask<IReadOnlyList<IBoardCell>> Show(
            IReadOnlyLifetime stateLifetime,
            IReadOnlyLifetime selectionLifetime,
            ICardDropPattern pattern);
    }
    
    public class CardDropArea : ICardDropArea
    {
        public CardDropArea(
            IUpdater updater,
            IGameInput input,
            IGameContext gameContext,
            ICardContext context)
        {
            _updater = updater;
            _input = input;
            _gameContext = gameContext;
            _context = context;
        }

        private readonly IUpdater _updater;
        private readonly IGameInput _input;
        private readonly IGameContext _gameContext;
        private readonly ICardContext _context;

        public async UniTask<IReadOnlyList<IBoardCell>> Show(
            IReadOnlyLifetime stateLifetime,
            IReadOnlyLifetime selectionLifetime,
            ICardDropPattern pattern)
        {
            IReadOnlyList<IBoardCell> previousData = null;

            await _updater.RunUpdateAction(
                stateLifetime,
                () => selectionLifetime.IsTerminated == false,
                _ =>
                {
                    var board = GetSelectedBoard();

                    if (board == null || _context.TargetBoard != board)
                    {
                        DeselectAll();
                        previousData = null;
                        return;
                    }

                    var boardPosition = board.WorldToBoardPosition(_input.World);
                    var dropData = pattern.GetDropData(boardPosition);

                    if (previousData != null)
                    {
                        foreach (var cell in previousData)
                        {
                            if (dropData.Contains(cell) == false)
                                cell.Selection.Deselect();
                        }
                    }

                    foreach (var cell in dropData)
                    {
                        if (previousData == null || previousData.Contains(cell) == false)
                            cell.Selection.Select();
                    }

                    previousData = dropData;
                });

            DeselectAll();

            return previousData;

            IBoard GetSelectedBoard()
            {
                var input = _input.World;

                foreach (var player in _gameContext.All)
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

                foreach (var cell in previousData)
                    cell.Selection.Deselect();
            }
        }
    }
}