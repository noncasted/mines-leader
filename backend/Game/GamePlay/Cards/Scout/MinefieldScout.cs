using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public class MinefieldScout : ICard<CardUsePayload.MinefieldScout>
{
    public MinefieldScout(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(CardUseContext context, CardUsePayload.MinefieldScout payload)
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

        var size = _configs.Value.MinefieldScout_Normal.Size;
        var horizontalPattern = PatternShapes.Line(size, horizontal: true);
        var verticalPattern = PatternShapes.Line(size, horizontal: false);

        var horizontalCells = horizontalPattern.SelectTaken(board, payload.Position);
        var verticalCells = verticalPattern.SelectTaken(board, payload.Position);

        var selected = horizontalCells.Count >= verticalCells.Count ? horizontalCells : verticalCells;

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No cells in the line pattern")
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

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.MinefieldScout()
        {
            TargetPlayer = board.OwnerId,
            RevealedCells = selected.Select(c => c.Position).ToList(),
            TargetCells = selected.Select(c => c.Position).ToList(),
            OpenedCells = openedCells,
            UpdatedFreeCells = openedCells,
            FlaggedCells = flagged
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}