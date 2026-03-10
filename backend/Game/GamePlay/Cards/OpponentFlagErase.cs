using Shared;

namespace Game.GamePlay;

public class OpponentFlagErase : ICard
{
    public OpponentFlagErase(
        IBoard target,
        CardConfigOptions.OpponentFlagErase config,
        CardUsePayload.OpponentFlagErase payload)
    {
        _target = target;
        _config = config;
        _payload = payload;
    }

    private readonly IBoard _target;
    private readonly CardConfigOptions.OpponentFlagErase _config;
    private readonly CardUsePayload.OpponentFlagErase _payload;

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
        
        var size = _config.Size;
        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectTaken(_target, _payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No free cells in the pattern"),
                ActionData = null
            };
        }

        var flagged = Enumerable.Where<ITakenCell>(selected, cell => cell.IsFlagged == true);

        foreach (var cell in flagged)
            cell.RemoveFlag();

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.OpponentFlagErase()
            {
                TargetPlayer = _target.OwnerId
            }
        };
    }
}