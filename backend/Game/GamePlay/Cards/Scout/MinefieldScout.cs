using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class MinefieldScout : ICard<CardUsePayload.MinefieldScout>
{
    public MinefieldScout(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.MinefieldScout payload)
    {
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells"),
                ActionData = null
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
                Result = EmptyResponse.Fail("No cells in the line pattern"),
                ActionData = null
            };
        }

        var revealed = new List<Position>();

        foreach (var cell in selected)
        {
            if (cell.HasMine)
            {
                cell.SetFlag();
            }
            else
            {
                cell.ToFree();
                board.Revealer.Reveal(cell.Position);
            }

            revealed.Add(cell.Position);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.MinefieldScout()
            {
                TargetPlayer = board.OwnerId,
                RevealedCells = revealed,
                TargetCells = selected.Select(c => c.Position).ToList()
            }
        };
    }
}