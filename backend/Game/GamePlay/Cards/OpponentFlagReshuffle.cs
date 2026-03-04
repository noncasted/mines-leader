using Shared;

namespace Game.GamePlay;

public class OpponentFlagReshuffle : ICard
{
    public OpponentFlagReshuffle(
        IBoard target,
        CardConfigOptions.OpponentFlagReshuffle config,
        CardUsePayload.OpponentFlagReshuffle payload)
    {
        _target = target;
        _config = config;
        _payload = payload;
    }

    private readonly IBoard _target;
    private readonly CardConfigOptions.OpponentFlagReshuffle _config;
    private readonly CardUsePayload.OpponentFlagReshuffle _payload;

    public CardUseResult Use()
    {
        if (_target.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Target board has no cells"),
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

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.OpponentFlagReshuffle()
            {
                TargetPlayer = _target.OwnerId
            }
        };
    }
}