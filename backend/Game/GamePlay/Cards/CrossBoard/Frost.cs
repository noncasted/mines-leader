using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Freezes cells in a diamond area on the opponent's field, preventing them from being opened or flagged.
/// </summary>
public class Frost : ICard<CardUsePayload.Frost>
{
    public Frost(ICardConfigs configs, IRoundActionService roundActionService, IGameContext gameContext)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Frost payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(context.Snapshot, payload.Position);

        var config = _configs.Value.Frost_Normal;
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
        var frozenPositions = new List<Position>();

        foreach (var cell in selected)
        {
            var effect = new FrostEffect { Id = effectId };
            cell.AddEffect(effect);
            affectedCells.Add(cell);
            frozenPositions.Add(cell.Position);
        }

        _roundActionService.Schedule(new FrostDisposeAction(board, effectId, affectedCells, config.TurnsDuration));

        var affectedPositions = affectedCells.Select(c => c.Position).ToArray();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Frost()
        {
            TargetPlayer = board.OwnerId,
            FrozenCells = frozenPositions,
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

public class FrostDisposeAction : IRoundAction
{
    public FrostDisposeAction(IBoard board, Guid effectId, List<ICell> cells, int roundsLeft)
    {
        _board = board;
        _effectId = effectId;
        _cells = cells;
        _roundsLeft = roundsLeft;
    }

    private readonly IBoard _board;
    private readonly Guid _effectId;
    private readonly List<ICell> _cells;
    private int _roundsLeft;

    public Guid OwnerId => _board.OwnerId;

    public bool Tick(MoveSnapshot snapshot)
    {
        _roundsLeft--;
        if (_roundsLeft > 0)
            return false;

        foreach (var cell in _cells)
        {
            cell.RemoveEffect(_effectId);
            snapshot.RecordEffectRemoved(_board, cell.Position, _effectId);
        }
        return true;
    }
}

public class FrostEffect : ICellEffect
{
    public required Guid Id { get; init; }

    public CellEffectType Type => CellEffectType.Frost;
}