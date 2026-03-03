using Shared;

namespace Game.GamePlay;

public class ErosionDozer : ICard
{
    public ErosionDozer(
        IBoard target,
        CardConfigOptions.ErosionDozer config,
        CardUsePayload.ErosionDozer payload)
    {
        _target = target;
        _config = config;
        _payload = payload;
    }

    private readonly IBoard _target;
    private readonly CardConfigOptions.ErosionDozer _config;
    private readonly CardUsePayload.ErosionDozer _payload;

    public EmptyResponse Use()
    {
        var size = _config.Size;

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