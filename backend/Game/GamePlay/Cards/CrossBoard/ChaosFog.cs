using Shared;

namespace Game.GamePlay;

/// <summary>
/// Covers a random-size diamond area on the opponent's field with smoke for a configured number of rounds.
/// </summary>
public class ChaosFog : ICard {
    public ChaosFog(IBoard target, CardUsePayload.ChaosFog payload, CardConfigOptions.ChaosFog config, IRoundActionService roundActionService, IPlayer owner, IGameRandom gameRandom) {
        _target = target;
        _payload = payload;
        _config = config;
        _roundActionService = roundActionService;
        _owner = owner;
        _gameRandom = gameRandom;
    }

    private readonly IBoard _target;
    private readonly CardUsePayload.ChaosFog _payload;
    private readonly CardConfigOptions.ChaosFog _config;
    private readonly IRoundActionService _roundActionService;
    private readonly IPlayer _owner;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use() {
        var actualSize = _gameRandom.Range(_owner, _config.MinSize, _config.MaxSize);
        var pattern = PatternShapes.Rhombus(actualSize);
        var selected = pattern.SelectAll(_target, _payload.Position);

        var effectId = Guid.NewGuid();
        var affectedCells = new List<ICell>();

        foreach (var cell in selected) {
            var effect = new ChaosFogEffect { Id = effectId };
            cell.AddEffect(effect);
            affectedCells.Add(cell);
        }

        var disposeAction = new ChaosFogDisposeAction(effectId, affectedCells);
        _roundActionService.Schedule(disposeAction, _config.Duration);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ChaosFog() {
                TargetPlayer = _target.OwnerId,
                ActualSize = actualSize
            }
        };
    }
}

public class ChaosFogDisposeAction : IRoundAction {
    public ChaosFogDisposeAction(Guid effectId, List<ICell> affectedCells) {
        _effectId = effectId;
        _affectedCells = affectedCells;
    }

    private readonly Guid _effectId;
    private readonly List<ICell> _affectedCells;

    public void Execute() {
        foreach (var cell in _affectedCells)
            cell.RemoveEffect(_effectId);
    }
}

public class ChaosFogEffect : ICellEffect {
    public required Guid Id { get; init; }
    public CellEffectType Type => CellEffectType.Smoke;
}
