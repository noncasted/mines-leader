using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Highlights mine positions within a diamond area without flagging or changing board state.
/// </summary>
public class ThermalVision : ICard<CardUsePayload.ThermalVision> {
    public ThermalVision(ICardConfigs configs) {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.ThermalVision payload) {
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        var pattern = PatternShapes.Rhombus(_configs.Value.ThermalVision_Normal.Size);
        var selected = pattern.SelectTaken(board, payload.Position);

        var mines = new List<Position>();
        foreach (var cell in selected) {
            if (cell.HasMine)
                mines.Add(cell.Position);
        }

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ThermalVision() {
                TargetPlayer = board.OwnerId,
                HighlightedMines = mines
            }
        };
    }
}
