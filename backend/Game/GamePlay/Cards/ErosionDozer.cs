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
        var config = _payload.Type.ToConfig();
        var size = config.Size;

        var selected = _target.GetClosedShape(_payload.Position);
        var ordered = selected.OrderBy(t => t.Position.DistanceTo(_payload.Position));

        var limited = ordered.Take(size).ToList();

        if (limited.Count == 0)
            return EmptyResponse.Fail("No taken cells in the pattern");

        foreach (var cell in limited)
            cell.ToFree();

        foreach (var cell in limited)
            _target.Revealer.Reveal(cell.Position);

        return EmptyResponse.Ok;
    }
}