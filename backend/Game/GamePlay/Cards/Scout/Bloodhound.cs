using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public class Bloodhound : ICard<CardUsePayload.Bloodhound>
{
    public Bloodhound(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Bloodhound payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        var config = payload.Type == CardType.Bloodhound_Max
            ? _configs.Value.BloodHound_Max
            : _configs.Value.BloodHound_Normal;
        var pattern = PatternShapes.Rhombus(config.Size);

        var selected = pattern.SelectTaken(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No taken cells in the pattern")
            };
        }

        var targetPositions = selected.Select(c => c.Position).ToList();
        var minePositions = new List<Position>();

        foreach (var cell in selected)
        {
            if (cell.HasMine == false)
                continue;

            cell.Explode();
            cell.ToFree();
            minePositions.Add(cell.Position);
        }

        var revealed = board.Revealer.Reveal(targetPositions);
        revealed.AddRange(minePositions);

        snapshot.RecordBoardStateUpdate(board);

        var openedCells = revealed.Distinct().Select(p => new OpenedCell
        {
            Position = p,
            MinesAround = board.Cells[p].AsFree().MinesAround
        }).ToList();

        var updatedPositions = revealed.Concat(board.GetFreeNeighbours(minePositions)).Distinct();

        var updatedFreeCells = updatedPositions.Select(p => new OpenedCell
        {
            Position = p,
            MinesAround = board.Cells[p].AsFree().MinesAround
        }).ToList();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Bloodhound()
        {
            TargetPlayer = board.OwnerId,
            TargetCells = targetPositions,
            OpenedCells = openedCells,
            UpdatedFreeCells = updatedFreeCells,
            ExplodedCells = minePositions
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}