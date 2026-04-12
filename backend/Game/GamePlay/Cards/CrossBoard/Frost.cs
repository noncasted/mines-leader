using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Freezes cells in a diamond area on the opponent's field, preventing them from being opened or flagged.
/// </summary>
public class Frost : ICard<CardUsePayload.Frost> {
    public Frost(ICardConfigs configs, IRoundActionService roundActionService, IGameContext gameContext) {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Frost payload) {
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        var config = _configs.Value.Frost_Normal;
        var pattern = PatternShapes.Rhombus(config.Size);
        var selected = pattern.SelectAll(board, payload.Position);

        if (selected.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("No cells in the pattern"),
                ActionData = null
            };
        }

        var effectId = Guid.NewGuid();
        var affectedCells = new List<ICell>();
        var frozenPositions = new List<Position>();

        foreach (var cell in selected) {
            var effect = new FrostEffect { Id = effectId };
            cell.AddEffect(effect);
            affectedCells.Add(cell);
            frozenPositions.Add(cell.Position);
        }

        _roundActionService.Schedule(new FrostDisposeAction(effectId, affectedCells), config.Duration);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Frost() {
                TargetPlayer = board.OwnerId,
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
