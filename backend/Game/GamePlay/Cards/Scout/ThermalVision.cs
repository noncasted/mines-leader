using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Highlights mine positions within a diamond area without flagging or changing board state.
/// </summary>
public class ThermalVision : ICard<CardUsePayload.ThermalVision>
{
    public ThermalVision(ICardConfigs configs, IRoundActionService roundActionService)
    {
        _configs = configs;
        _roundActionService = roundActionService;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.ThermalVision payload)
    {
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        var pattern = PatternShapes.Rhombus(_configs.Value.ThermalVision_Normal.Size);
        var selected = pattern.SelectTaken(board, payload.Position);

        var effectId = Guid.NewGuid();
        var mines = new List<Position>();
        var affectedCells = new List<ICell>();

        foreach (var cell in selected)
        {
            if (cell.HasMine)
            {
                mines.Add(cell.Position);
                cell.AddEffect(new MineHighlightEffect { Id = effectId });
                affectedCells.Add(cell);
            }
        }

        if (affectedCells.Count > 0)
            _roundActionService.Schedule(new MineHighlightDisposeAction(effectId, affectedCells), 2);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ThermalVision()
            {
                TargetPlayer = board.OwnerId,
                HighlightedMines = mines
            }
        };
    }
}

public class MineHighlightEffect : ICellEffect
{
    public required Guid Id { get; init; }
    public CellEffectType Type => CellEffectType.MineHighlight;
}

public class MineHighlightDisposeAction : IRoundAction
{
    public MineHighlightDisposeAction(Guid effectId, List<ICell> cells)
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