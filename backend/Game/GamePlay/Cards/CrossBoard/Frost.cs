using Shared;

namespace Game.GamePlay;

/// <summary>
/// Freezes cells in a diamond area on the opponent's field, preventing them from being opened or flagged.
/// </summary>
public class Frost : ICard
{
    public Frost(
        IBoard target,
        CardConfigOptions.Frost config,
        CardUsePayload.Frost payload,
        IRoundActionService roundActionService)
    {
        _target = target;
        _config = config;
        _payload = payload;
        _roundActionService = roundActionService;
    }

    private readonly IBoard _target;
    private readonly CardConfigOptions.Frost _config;
    private readonly CardUsePayload.Frost _payload;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use()
    {
        var pattern = PatternShapes.Rhombus(_config.Size);
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
        var frozenPositions = new List<Position>();

        foreach (var cell in selected)
        {
            var effect = new FrostEffect { Id = effectId };
            cell.AddEffect(effect);
            affectedCells.Add(cell);
            frozenPositions.Add(cell.Position);
        }

        _roundActionService.Schedule(new FrostDisposeAction(effectId, affectedCells), _config.Duration);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Frost()
            {
                TargetPlayer = _target.OwnerId,
                FrozenCells = frozenPositions
            }
        };
    }
}

public class FrostDisposeAction : IRoundAction
{
    public FrostDisposeAction(Guid effectId, List<ICell> cells)
    {
        _effectId = effectId;
        _cells = cells;
    }

    private readonly Guid _effectId;
    private readonly List<ICell> _cells;

    public void Execute()
    {
        foreach (var cell in _cells)
            cell.RemoveEffect(_effectId);
    }
}

public class FrostEffect : ICellEffect
{
    public required Guid Id { get; init; }

    public CellEffectType Type => CellEffectType.Frost;
}
