using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Targets a cross-shaped area: flags mines and reveals safe cells within the pattern.
/// </summary>
public class Excavator : ICard<CardUsePayload.Excavator>
{
    public Excavator(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Excavator payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        var pattern = PatternShapes.Cross(_configs.Value.Excavator_Normal.Size);
        var selected = pattern.SelectTaken(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No taken cells in the cross pattern")
            };
        }

        var toReveal = new List<Position>();
        var flagged = new List<Position>();

        foreach (var cell in selected)
        {
            if (cell.HasMine)
            {
                cell.SetFlag();
                flagged.Add(cell.Position);
            }
            else
            {
                toReveal.Add(cell.Position);
            }
        }

        var revealed = board.Revealer.Reveal(toReveal);

        var openedCells = revealed.Distinct().Select(p => new OpenedCell
        {
            Position = p,
            MinesAround = board.Cells[p].AsFree().MinesAround
        }).ToList();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Excavator()
        {
            TargetPlayer = board.OwnerId,
            TargetCells = selected.Select(c => c.Position).ToList(),
            OpenedCells = openedCells,
            UpdatedFreeCells = openedCells
        });

        foreach (var position in flagged)
            snapshot.RecordFlag(board, position, true);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}