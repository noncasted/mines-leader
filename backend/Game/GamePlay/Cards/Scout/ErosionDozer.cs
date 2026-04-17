using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public class ErosionDozer : ICard<CardUsePayload.ErosionDozer>
{
    public ErosionDozer(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(CardUseContext context, CardUsePayload.ErosionDozer payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        var size = _configs.Value.ErosionDozer_Normal.Size;

        var selected = board.GetClosedShape(payload.Position);
        var ordered = selected.OrderBy(t => t.Position.DistanceTo(payload.Position));

        var limited = ordered.Take(size).ToList();

        if (limited.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No taken cells in the pattern")
            };
        }

        var targetPositions = limited.Select(c => c.Position).ToList();
        var minePositions = new List<Position>();

        foreach (var cell in limited)
        {
            var taken = cell.AsTaken();

            if (taken.HasMine == false)
                continue;

            snapshot.RecordExplosion(board, taken.Position);
            taken.Explode();
            taken.ToFree();
            minePositions.Add(taken.Position);
        }

        var revealed = board.Revealer.Reveal(targetPositions);
        revealed.AddRange(minePositions);

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

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.ErosionDozer()
        {
            TargetPlayer = board.OwnerId,
            TargetCells = targetPositions,
            OpenedCells = openedCells,
            UpdatedFreeCells = updatedFreeCells
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}