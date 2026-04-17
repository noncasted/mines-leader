using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public class FogOfWar : ICard<CardUsePayload.FogOfWar>
{
    public FogOfWar(ICardConfigs configs, IRoundActionService roundActionService, IGameContext gameContext)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.FogOfWar payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells")
            };
        }

        var config = _configs.Value.FogOfWar_Normal;
        var pattern = PatternShapes.Rhombus(config.Size);
        var selected = pattern.SelectFree(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No free cells in the pattern")
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

        var disposeAction = new FogDisposeAction(board, effectId, affectedCells);
        _roundActionService.Schedule(disposeAction, config.Duration);

        var affectedPositions = affectedCells.Select(c => c.Position).ToArray();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.FogOfWar()
        {
            TargetPlayer = board.OwnerId,
            AffectedCells = affectedPositions,
            EffectId = effectId,
            TargetCells = affectedPositions
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}

public class FogDisposeAction : IRoundAction
{
    public FogDisposeAction(IBoard board, Guid effectId, List<ICell> affectedCells)
    {
        _board = board;
        _effectId = effectId;
        _affectedCells = affectedCells;
    }

    private readonly IBoard _board;
    private readonly Guid _effectId;
    private readonly List<ICell> _affectedCells;

    public void Execute(MoveSnapshot snapshot)
    {
        foreach (var cell in _affectedCells)
        {
            cell.RemoveEffect(_effectId);
            snapshot.RecordEffectRemoved(_board, cell.Position, _effectId);
        }
    }
}

public class FogEffect : ICellEffect
{
    public required Guid Id { get; init; }

    public CellEffectType Type => CellEffectType.Fog;
}