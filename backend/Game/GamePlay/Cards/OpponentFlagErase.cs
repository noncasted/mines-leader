using Shared;

namespace Game.GamePlay;

public class OpponentFlagErase : ICard
{
    public OpponentFlagErase(
        IBoard target,
        CardUsePayload.OpponentFlagErase payload)
    {
        _target = target;
        _payload = payload;
    }

    private readonly IBoard _target;
    private readonly CardUsePayload.OpponentFlagErase _payload;

    public EmptyResponse Use()
    {
        var config = _payload.Type.ToConfig();
        var size = config.Size;
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