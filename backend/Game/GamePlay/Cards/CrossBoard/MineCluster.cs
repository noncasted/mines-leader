using Shared;

namespace Game.GamePlay;

/// <summary>
/// Plants mines in a cross-shaped pattern on free cells of the opponent's field.
/// </summary>
public class MineCluster : ICard {
    public MineCluster(IPlayer owner, IBoard target, CardConfigOptions.MineCluster config, CardUsePayload.MineCluster payload) {
        _owner = owner;
        _target = target;
        _config = config;
        _payload = payload;
    }

    private readonly IPlayer _owner;
    private readonly IBoard _target;
    private readonly CardConfigOptions.MineCluster _config;
    private readonly CardUsePayload.MineCluster _payload;

    public CardUseResult Use() {
        var pattern = PatternShapes.Cross(_config.Size);
        var selected = pattern.SelectFree(_target, _payload.Position);

        if (selected.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("No free cells in the cross pattern"),
                ActionData = null
            };
        }

        foreach (var cell in selected)
            cell.ToTaken();

        foreach (var cell in selected)
            cell.ToTaken().SetMine();

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.MineCluster() {
                TargetPlayer = _target.OwnerId
            }
        };
    }
}
