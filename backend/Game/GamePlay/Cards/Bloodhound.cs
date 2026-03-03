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

    public EmptyResponse Use()
    {
        var size = _config.Size;
        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectTaken(_target, _payload.Position);

        if (selected.Count == 0)
            return EmptyResponse.Fail("No taken cells in the pattern");

        foreach (var cell in selected)
            cell.ToFree();

        foreach (var cell in selected)
            _target.Revealer.Reveal(cell.Position);

        return EmptyResponse.Ok;
    }
}