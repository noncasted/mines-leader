using Shared;

namespace Game.GamePlay;

public class Smoke : ICard
{
    public Smoke(
        IBoard target,
        CardUsePayload.Smoke payload,
        IRoundActionService roundActionService)
    {
        _target = target;
        _payload = payload;
        _roundActionService = roundActionService;
    }

    private readonly IBoard _target;
    private readonly CardUsePayload.Smoke _payload;
    private readonly IRoundActionService _roundActionService;

    public EmptyResponse Use()
    {
        var config = _payload.Type.ToConfig<CardsConfigs.ISmoke>();
        var size = config.Size;

        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectAll(_target, _payload.Position);

        if (selected.Count == 0)
            return EmptyResponse.Fail("No cells in the pattern");

        var effectId = Guid.NewGuid();
        var affectedCells = new List<ICell>();

        foreach (var cell in selected)
        {
            var effect = new SmokeEffect { Id = effectId };
            cell.AddEffect(effect);
            affectedCells.Add(cell);
        }

        var duration = config.Duration;
        var disposeAction = new SmokeDisposeAction(effectId, affectedCells);
        _roundActionService.Schedule(disposeAction, duration);

        return EmptyResponse.Ok;
    }
}

public class SmokeDisposeAction : IRoundAction
{
    public SmokeDisposeAction(Guid effectId, List<ICell> affectedCells)
    {
        _effectId = effectId;
        _affectedCells = affectedCells;
    }

    private readonly Guid _effectId;
    private readonly List<ICell> _affectedCells;

    public void Execute()
    {
        foreach (var cell in _affectedCells)
            cell.RemoveEffect(_effectId);
    }
}

public class SmokeEffect : ICellEffect
{
    public required Guid Id { get; init; }
    
    public CellEffectType Type => CellEffectType.Smoke;
}