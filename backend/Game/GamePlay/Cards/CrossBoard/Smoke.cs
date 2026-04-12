using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class Smoke : ICard<CardUsePayload.Smoke> {
    public Smoke(ICardConfigs configs, IRoundActionService roundActionService, IGameContext gameContext) {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Smoke payload) {
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("TargetId board has no cells"),
                ActionData = null
            };
        }

        var config = _configs.Value.Smoke_Normal;
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

        foreach (var cell in selected) {
            var effect = new SmokeEffect { Id = effectId };
            cell.AddEffect(effect);
            affectedCells.Add(cell);
        }

        var disposeAction = new SmokeDisposeAction(effectId, affectedCells);
        _roundActionService.Schedule(disposeAction, config.Duration);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Smoke() {
                TargetPlayer = board.OwnerId
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
