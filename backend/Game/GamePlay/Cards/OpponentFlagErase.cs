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

    public EmptyResponse Use()
    {
        var size = _config.Size;
        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectTaken(_target, _payload.Position);

        if (selected.Count == 0)
            return EmptyResponse.Fail("No free cells in the pattern");
        
        var flagged = Enumerable.Where<ITakenCell>(selected, cell => cell.IsFlagged == true);

        foreach (var cell in flagged)
            cell.RemoveFlag();
        
        return EmptyResponse.Ok;
    }
}