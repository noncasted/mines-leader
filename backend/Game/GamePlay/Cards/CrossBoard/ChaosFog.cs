using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Covers a random-size diamond area on the opponent's field with smoke for a configured number of rounds.
/// </summary>
public class ChaosFog : ICard<CardUsePayload.ChaosFog>
{
    public ChaosFog(
        ICardConfigs configs,
        IRoundActionService roundActionService,
        IGameContext gameContext,
        IGameRandom gameRandom)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
        _gameRandom = gameRandom;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use(CardUseContext context, CardUsePayload.ChaosFog payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(context.Snapshot, payload.Position);

        var config = _configs.Value.ChaosFog_Normal;
        var actualSize = _gameRandom.Range(invoker, config.MinSize, config.MaxSize);
        var pattern = PatternShapes.Rhombus(actualSize);
        var selected = pattern.SelectAll(board, payload.Position);

        var effectId = Guid.NewGuid();
        var affectedCells = new List<ICell>();

        foreach (var cell in selected)
        {
            var effect = new ChaosFogEffect { Id = effectId };
            cell.AddEffect(effect);
            affectedCells.Add(cell);
        }

        var disposeAction = new ChaosFogDisposeAction(board, effectId, affectedCells, config.TurnsDuration);
        _roundActionService.Schedule(disposeAction);

        var affectedPositions = affectedCells.Select(c => c.Position).ToArray();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.ChaosFog()
        {
            TargetPlayer = board.OwnerId,
            ActualSize = actualSize,
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

public class ChaosFogDisposeAction : IRoundAction
{
    public ChaosFogDisposeAction(IBoard board, Guid effectId, List<ICell> affectedCells, int roundsLeft)
    {
        _board = board;
        _effectId = effectId;
        _affectedCells = affectedCells;
        _roundsLeft = roundsLeft;
    }

    private readonly IBoard _board;
    private readonly Guid _effectId;
    private readonly List<ICell> _affectedCells;
    private int _roundsLeft;

    public Guid OwnerId => _board.OwnerId;

    public bool Tick(MoveSnapshot snapshot)
    {
        _roundsLeft--;
        if (_roundsLeft > 0)
            return false;

        foreach (var cell in _affectedCells)
        {
            cell.RemoveEffect(_effectId);
            snapshot.RecordEffectRemoved(_board, cell.Position, _effectId);
        }
        return true;
    }
}

public class ChaosFogEffect : ICellEffect
{
    public required Guid Id { get; init; }
    public CellEffectType Type => CellEffectType.Smoke;
}