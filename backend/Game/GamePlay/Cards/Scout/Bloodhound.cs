using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class Bloodhound : ICard<CardUsePayload.Bloodhound> {
    public Bloodhound(ICardConfigs configs) {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Bloodhound payload) {
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        var size = _configs.Value.BloodHound_Normal.Size;
        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectTaken(board, payload.Position);

        if (selected.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("No taken cells in the pattern"),
                ActionData = null
            };
        }

        foreach (var cell in selected)
            cell.ToFree();

        foreach (var cell in selected)
            board.Revealer.Reveal(cell.Position);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Bloodhound() {
                TargetPlayer = board.OwnerId
            }
        };
    }
}
