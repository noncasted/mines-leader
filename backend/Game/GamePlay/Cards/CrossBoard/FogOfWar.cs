using Shared;

namespace Game.GamePlay;

public class FogOfWar : ICard
{
    public FogOfWar(
        IBoard target,
        CardUsePayload.FogOfWar payload,
        CardConfigOptions.FogOfWar config,
        IRoundActionService roundActionService)
    {
        _target = target;
        _payload = payload;
        _config = config;
        _roundActionService = roundActionService;
    }

    private readonly IBoard _target;
    private readonly CardUsePayload.FogOfWar _payload;
    private readonly CardConfigOptions.FogOfWar _config;
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

        var pattern = PatternShapes.Rhombus(_config.Size);
        var selected = pattern.SelectFree(_target, _payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No free cells in the pattern"),
                ActionData = null
            };
        }

        var effectId = Guid.NewGuid();
        var affectedCells = new List<ICell>();

        foreach (var cell in selected)
        {
            var effect = new FogEffect { Id = effectId };
            cell.AddEffect(effect);
            affectedCells.Add(cell);
        }

        var disposeAction = new FogDisposeAction(effectId, affectedCells);
        _roundActionService.Schedule(disposeAction, _config.Duration);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.FogOfWar()
            {
                TargetPlayer = _target.OwnerId
            }
        };
    }
}

public class FogDisposeAction : IRoundAction
{
    public FogDisposeAction(Guid effectId, List<ICell> affectedCells)
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

public class FogEffect : ICellEffect
{
    public required Guid Id { get; init; }

    public CellEffectType Type => CellEffectType.Fog;
}