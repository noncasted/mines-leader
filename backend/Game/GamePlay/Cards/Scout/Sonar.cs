using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class Sonar : ICard<CardUsePayload.Sonar> {
    public Sonar(ICardConfigs configs) {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Sonar payload) {
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("TargetId board has no cells"),
                ActionData = null
            };
        }

        var pattern = PatternShapes.Rhombus(_configs.Value.Sonar_Normal.Size);
        var selected = pattern.SelectTaken(board, payload.Position);
        var mines = selected.Where(cell => cell.HasMine && !cell.IsFlagged).ToList();
        var flaggedPositions = new List<Position>();

        foreach (var cell in mines) {
            cell.SetFlag();
            flaggedPositions.Add(cell.Position);
        }

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Sonar {
                TargetPlayer = board.OwnerId,
                FlaggedCells = flaggedPositions
            }
        };
    }
}
