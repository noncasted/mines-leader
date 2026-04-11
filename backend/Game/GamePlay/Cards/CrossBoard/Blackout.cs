using Shared;

namespace Game.GamePlay;

/// <summary>
/// Hides mine numbers on cells in a diamond area on the opponent's field for a configured number of rounds.
/// </summary>
public class Blackout : ICard
{
    public Blackout(
        IBoard target,
        CardConfigOptions.Blackout config,
        CardUsePayload.Blackout payload,
        IRoundActionService roundActionService)
    {
        _target = target;
        _config = config;
        _payload = payload;
        _roundActionService = roundActionService;
    }

    private readonly IBoard _target;
    private readonly CardConfigOptions.Blackout _config;
    private readonly CardUsePayload.Blackout _payload;
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
        var affectedPositions = new List<Position>();

        foreach (var cell in selected)
        {
            var effect = new BlackoutEffect { Id = effectId };
            cell.AddEffect(effect);
            affectedCells.Add(cell);
            affectedPositions.Add(cell.Position);
        }

        _roundActionService.Schedule(new BlackoutDisposeAction(effectId, affectedCells), _config.Duration);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Blackout()
            {
                TargetPlayer = _target.OwnerId,
                AffectedCells = affectedPositions
            }
        };
    }
}

public class BlackoutDisposeAction : IRoundAction
{
    public BlackoutDisposeAction(Guid effectId, List<ICell> cells)
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

public class BlackoutEffect : ICellEffect
{
    public required Guid Id { get; init; }

    public CellEffectType Type => CellEffectType.Blackout;
}
