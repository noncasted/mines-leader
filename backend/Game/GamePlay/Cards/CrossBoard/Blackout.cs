using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Hides mine numbers on cells in a diamond area on the opponent's field for a configured number of rounds.
/// </summary>
public class Blackout : ICard<CardUsePayload.Blackout>
{
    public Blackout(ICardConfigs configs, IRoundActionService roundActionService, IGameContext gameContext)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Blackout payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        var config = _configs.Value.Blackout_Normal;
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
        var affectedPositions = new List<Position>();

        foreach (var cell in selected)
        {
            var effect = new BlackoutEffect { Id = effectId };
            cell.AddEffect(effect);
            affectedCells.Add(cell);
            affectedPositions.Add(cell.Position);
        }

        _roundActionService.Schedule(new BlackoutDisposeAction(board, effectId, affectedCells), config.Duration);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Blackout()
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

public class BlackoutDisposeAction : IRoundAction
{
    public BlackoutDisposeAction(IBoard board, Guid effectId, List<ICell> cells)
    {
        _board = board;
        _effectId = effectId;
        _cells = cells;
    }

    private readonly IBoard _board;
    private readonly Guid _effectId;
    private readonly List<ICell> _cells;

    public void Execute(MoveSnapshot snapshot)
    {
        foreach (var cell in _cells)
        {
            cell.RemoveEffect(_effectId);
            snapshot.RecordEffectRemoved(_board, cell.Position, _effectId);
        }
    }
}

public class BlackoutEffect : ICellEffect
{
    public required Guid Id { get; init; }

    public CellEffectType Type => CellEffectType.Blackout;
}