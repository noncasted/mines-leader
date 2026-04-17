using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public class OpponentFlagErase : ICard<CardUsePayload.OpponentFlagErase>
{
    public OpponentFlagErase(ICardConfigs configs, IGameContext gameContext)
    {
        _configs = configs;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.OpponentFlagErase payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells")
            };
        }

        var config = _configs.Value.OpponentFlagErase_Normal;
        var pattern = PatternShapes.Rhombus(config.Size);
        var selected = pattern.SelectTaken(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No free cells in the pattern")
            };
        }

        var flagged = selected.Where(cell => cell.IsFlagged == true).ToList();
        var removed = new List<Position>();

        foreach (var cell in flagged)
        {
            cell.RemoveFlag();
            removed.Add(cell.Position);
        }

        var minesRecords = board.MinesScanner.Recalculate(snapshot);

        var updatedFreeCells = minesRecords
                               .Select(r => new OpenedCell { Position = r.Position, MinesAround = r.Count })
                               .ToList();

        var selectedPositions = selected.Select(c => c.Position).ToList();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.OpponentFlagErase()
        {
            TargetPlayer = board.OwnerId,
            UnflaggedCells = removed,
            UpdatedFreeCells = updatedFreeCells,
            TargetCells = selectedPositions
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}