using System.Linq;
using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class Smoke : ICard<CardUsePayload.Smoke>
{
    public Smoke(ICardConfigs configs, IRoundActionService roundActionService, IGameContext gameContext)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Smoke payload)
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

        var config = _configs.Value.Smoke_Normal;
        var pattern = PatternShapes.Rhombus(config.Size);
        var selected = pattern.SelectAll(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No cells in the pattern")
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

        var disposeAction = new SmokeDisposeAction(board, effectId, affectedCells);
        _roundActionService.Schedule(disposeAction, config.Duration);

        var positions = affectedCells.Select(c => c.Position).ToArray();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Smoke()
        {
            TargetPlayer = board.OwnerId,
            TargetCells = positions,
            OpenedCells = positions,
            AffectedCells = positions,
            EffectId = effectId
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}

public class SmokeDisposeAction : IRoundAction
{
    public SmokeDisposeAction(IBoard board, Guid effectId, List<ICell> affectedCells)
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

public class SmokeEffect : ICellEffect
{
    public required Guid Id { get; init; }

    public CellEffectType Type => CellEffectType.Smoke;
}