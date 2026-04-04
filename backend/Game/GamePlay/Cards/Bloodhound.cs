using Shared;

namespace Game.GamePlay;

public class Bloodhound : ICard
{
    public Bloodhound(
        IBoard target,
        CardConfigOptions.Bloodhound config,
        CardUsePayload.Bloodhound payload)
    {
        _target = target;
        _config = config;
        _payload = payload;
    }

    private readonly IBoard _target;
    private readonly CardConfigOptions.Bloodhound _config;
    private readonly CardUsePayload.Bloodhound _payload;

    public CardUseResult Use()
    {
        var size = _config.Size;
        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectTaken(_target, _payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No taken cells in the pattern"),
                ActionData = null
            };
        }

        foreach (var cell in selected)
            cell.ToFree();

        foreach (var cell in selected)
            _target.Revealer.Reveal(cell.Position);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Bloodhound()
            {
                TargetPlayer = _target.OwnerId
            }
        };
    }
}