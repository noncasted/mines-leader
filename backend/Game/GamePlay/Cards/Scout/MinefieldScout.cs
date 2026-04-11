using Shared;

namespace Game.GamePlay;

public class MinefieldScout : ICard
{
    public MinefieldScout(
        IBoard target,
        CardUsePayload.MinefieldScout payload,
        CardConfigOptions.MinefieldScout config)
    {
        _target = target;
        _payload = payload;
        _config = config;
    }

    private readonly IBoard _target;
    private readonly CardUsePayload.MinefieldScout _payload;
    private readonly CardConfigOptions.MinefieldScout _config;

    public CardUseResult Use()
    {
        if (_target.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells"),
                ActionData = null
            };
        }

        var horizontalPattern = PatternShapes.Line(_config.Size, horizontal: true);
        var verticalPattern = PatternShapes.Line(_config.Size, horizontal: false);

        var horizontalCells = horizontalPattern.SelectTaken(_target, _payload.Position);
        var verticalCells = verticalPattern.SelectTaken(_target, _payload.Position);

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
                _target.Revealer.Reveal(cell.Position);
            }

            revealed.Add(cell.Position);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.MinefieldScout()
            {
                TargetPlayer = _target.OwnerId,
                RevealedCells = revealed
            }
        };
    }
}