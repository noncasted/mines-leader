using Shared;

namespace Game.GamePlay;

public class Smoke : ICard
{
    public Smoke(
        IBoard target,
        CardUsePayload.Smoke payload,
        CardConfigOptions.Smoke config,
        IRoundActionService roundActionService)
    {
        _target = target;
        _payload = payload;
        _config = config;
        _roundActionService = roundActionService;
    }

    private readonly IBoard _target;
    private readonly CardUsePayload.Smoke _payload;
    private readonly CardConfigOptions.Smoke _config;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use()
    {
        if (_target.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells"),
                ActionData = null
            };
        }
        
        var size = _config.Size;

        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectAll(_target, _payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No cells in the pattern"),
                ActionData = null
            };
        }

        var effectId = Guid.NewGuid();
        var affectedCells = new List<ICell>();

        foreach (var cell in selected)
        {
            var effect = new SmokeEffect { Id = effectId };
            cell.AddEffect(effect);
            affectedCells.Add(cell);
        }

        var duration = _config.Duration;
        var disposeAction = new SmokeDisposeAction(effectId, affectedCells);
        _roundActionService.Schedule(disposeAction, duration);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Smoke()
            {
                TargetPlayer = _target.OwnerId
            }
        };
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