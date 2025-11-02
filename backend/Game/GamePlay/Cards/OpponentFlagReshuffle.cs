using Shared;

namespace Game.GamePlay;

public class OpponentFlagReshuffle : ICard
{
    public OpponentFlagReshuffle(
        IBoard target,
        CardUsePayload.OpponentFlagReshuffle payload)
    {
        _target = target;
        _payload = payload;
    }

    private readonly IBoard _target;
    private readonly CardUsePayload.OpponentFlagReshuffle _payload;

    public EmptyResponse Use()
    {
        var config = _payload.Type.ToConfig();
        var size = config.Size;
        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectTaken(_target, _payload.Position);

        if (selected.Count == 0)
            return EmptyResponse.Fail("No free cells in the pattern");
        
        var flagged = Enumerable.Where<ITakenCell>(selected, cell => cell.IsFlagged == true).ToList();
        var notFlagged = Enumerable.Where<ITakenCell>(selected, cell => cell.IsFlagged == false).ToList();

        while (flagged.Count != 0 && notFlagged.Count != 0)
        {
            var firstFlagged = flagged.First();
            var randomNotFlaggedIndex = Random.Shared.Next(0, notFlagged.Count);
            var randomNotFlagged = notFlagged[randomNotFlaggedIndex];
            
            firstFlagged.RemoveFlag();
            randomNotFlagged.SetFlag();
            flagged.RemoveAt(0);
            notFlagged.RemoveAt(randomNotFlaggedIndex);
        }

        return EmptyResponse.Ok;
    }
}