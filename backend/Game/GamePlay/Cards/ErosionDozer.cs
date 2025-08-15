using Shared;

namespace Game.GamePlay;

public class ErosionDozer : ICard
{
    public ErosionDozer(
        IBoard target,
        CardUsePayload.ErosionDozer payload)
    {
        _target = target;
        _payload = payload;
    }

    private readonly IBoard _target;
    private readonly CardUsePayload.ErosionDozer _payload;

    public EmptyResponse Use()
    {
        var size = _payload.Type.GetSize();

        var selected = _target.GetClosedShape(_payload.Position);
        var ordered = selected.OrderBy(t => t.Position.DistanceTo(_payload.Position));

        var limited = ordered.Take(size).ToList();
        
        if (limited.Count == 0)
            return EmptyResponse.Fail("No taken cells in the pattern");

        foreach (var cell in selected)
            cell.ToFree();

        _target.Revealer.Reveal(_payload.Position);

        return EmptyResponse.Ok;
    }
}