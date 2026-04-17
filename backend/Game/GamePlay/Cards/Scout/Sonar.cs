using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class Sonar : ICard<CardUsePayload.Sonar>
{
    public Sonar(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Sonar payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells")
            };
        }

        var pattern = PatternShapes.Rhombus(_configs.Value.Sonar_Normal.Size);
        var selected = pattern.SelectTaken(board, payload.Position);
        var mines = selected.Where(cell => cell.HasMine && !cell.IsFlagged).ToList();

        if (mines.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No unflagged mines in the sonar range")
            };
        }

        var flaggedPositions = new List<Position>();

        foreach (var cell in mines)
        {
            cell.SetFlag();
            flaggedPositions.Add(cell.Position);
        }

        var minesRecords = board.MinesScanner.Recalculate(snapshot);
        var updatedFreeCells = minesRecords
            .Select(r => new OpenedCell { Position = r.Position, MinesAround = r.Count })
            .ToList();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Sonar
        {
            TargetPlayer = board.OwnerId,
            FlaggedCells = flaggedPositions,
            UpdatedFreeCells = updatedFreeCells,
            TargetCells = flaggedPositions
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}