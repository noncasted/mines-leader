using Shared;

namespace Game.GamePlay;

/// <summary>
/// Highlights mine positions within a diamond area without flagging or changing board state.
/// </summary>
public class ThermalVision : ICard {
    public ThermalVision(IBoard target, CardConfigOptions.ThermalVision config, CardUsePayload.ThermalVision payload) {
        _target = target;
        _config = config;
        _payload = payload;
    }

    private readonly IBoard _target;
    private readonly CardConfigOptions.ThermalVision _config;
    private readonly CardUsePayload.ThermalVision _payload;

    public CardUseResult Use() {
        var pattern = PatternShapes.Rhombus(_config.Size);
        var selected = pattern.SelectTaken(_target, _payload.Position);

        var mines = new List<Position>();
        foreach (var cell in selected) {
            if (cell.HasMine)
                mines.Add(cell.Position);
        }

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ThermalVision() {
                TargetPlayer = _target.OwnerId,
                HighlightedMines = mines
            }
        };
    }
}
