using Shared;
using Cluster.Configs;

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

    public CardUseResult Use(IPlayer invoker, CardUsePayload.ChaosFog payload)
    {
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

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

        var disposeAction = new ChaosFogDisposeAction(effectId, affectedCells);
        _roundActionService.Schedule(disposeAction, config.Duration);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ChaosFog()
            {
                TargetPlayer = board.OwnerId,
                ActualSize = actualSize
            }
        };
    }
}

public class ChaosFogDisposeAction : IRoundAction
{
    public ChaosFogDisposeAction(Guid effectId, List<ICell> affectedCells)
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

public class ChaosFogEffect : ICellEffect
{
    public required Guid Id { get; init; }
    public CellEffectType Type => CellEffectType.Smoke;
}