using Cluster.Configs;
using Shared;

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

    public CardUseResult Use(CardUseContext context, CardUsePayload.ThermalVision payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var board = invoker.Board;
        board.EnsureGenerated(context.Snapshot, payload.Position);

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
            _roundActionService.Schedule(new MineHighlightDisposeAction(board, effectId, affectedCells, 2));

        var affectedPositions = affectedCells.Select(c => c.Position).ToArray();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.ThermalVision()
        {
            TargetPlayer = board.OwnerId,
            HighlightedMines = mines,
            AffectedCells = affectedPositions,
            EffectId = effectId,
            TargetCells = selected.Select(t => t.Position).ToArray()
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
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
    public MineHighlightDisposeAction(IBoard board, Guid effectId, List<ICell> cells, int roundsLeft)
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