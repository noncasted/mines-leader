using System.Linq;
using Shared;

namespace Game.GamePlay;

public class Sonar : ICard
{
    public Sonar(IBoard target, CardConfigOptions.Sonar config, CardUsePayload.Sonar payload)
    {
        _target = target;
        _config = config;
        _payload = payload;
    }

    private readonly IBoard _target;
    private readonly CardConfigOptions.Sonar _config;
    private readonly CardUsePayload.Sonar _payload;

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

        var pattern = PatternShapes.Rhombus(_config.Size);
        var selected = pattern.SelectTaken(_target, _payload.Position);
        var mines = selected.Where(cell => cell.HasMine && !cell.IsFlagged).ToList();

        if (mines.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No unflagged mines in the pattern"),
                ActionData = null
            };
        }

        var flaggedPositions = new List<Position>();

        foreach (var cell in mines)
        {
            cell.SetFlag();
            flaggedPositions.Add(cell.Position);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Sonar
            {
                TargetPlayer = _target.OwnerId,
                FlaggedCells = flaggedPositions
            }
        };
    }
}
