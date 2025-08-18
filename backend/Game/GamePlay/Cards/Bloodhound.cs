using Shared;

namespace Game.GamePlay;

public class Bloodhound : ICard
{
    public Bloodhound(
        IPlayer owner,
        IBoard target,
        CardUsePayload.Bloodhound payload)
    {
        _owner = owner;
        _target = target;
        _payload = payload;
    }

    private readonly IPlayer _owner;
    private readonly IBoard _target;
    private readonly CardUsePayload.Bloodhound _payload;

    public EmptyResponse Use()
    {
        var size = _payload.Type.GetSize();
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