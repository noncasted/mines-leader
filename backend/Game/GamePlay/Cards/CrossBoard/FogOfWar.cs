using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class FogOfWar : ICard<CardUsePayload.FogOfWar> {
    public FogOfWar(ICardConfigs configs, IRoundActionService roundActionService, IGameContext gameContext) {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.FogOfWar payload) {
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("TargetId board has no cells"),
                ActionData = null
            };
        }

        var config = _configs.Value.FogOfWar_Normal;
        var pattern = PatternShapes.Rhombus(config.Size);
        var selected = pattern.SelectFree(board, payload.Position);

        if (selected.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("No free cells in the pattern"),
                ActionData = null
            };
        }

        var effectId = Guid.NewGuid();
        var affectedCells = new List<ICell>();

        foreach (var cell in selected) {
            var effect = new FogEffect { Id = effectId };
            cell.AddEffect(effect);
            affectedCells.Add(cell);
        }

        var disposeAction = new FogDisposeAction(effectId, affectedCells);
        _roundActionService.Schedule(disposeAction, config.Duration);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.FogOfWar() {
                TargetPlayer = board.OwnerId
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
