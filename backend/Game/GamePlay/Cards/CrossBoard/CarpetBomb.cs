using Shared;

namespace Game.GamePlay;

/// <summary>
/// Plants mines along the longest available line on free cells of the opponent's field.
/// </summary>
public class CarpetBomb : ICard {
    public CarpetBomb(IPlayer owner, IBoard target, CardConfigOptions.CarpetBomb config, CardUsePayload.CarpetBomb payload) {
        _owner = owner;
        _target = target;
        _config = config;
        _payload = payload;
    }

    private readonly IPlayer _owner;
    private readonly IBoard _target;
    private readonly CardConfigOptions.CarpetBomb _config;
    private readonly CardUsePayload.CarpetBomb _payload;

    public CardUseResult Use() {
        var horizontalPattern = PatternShapes.Line(_config.Length, horizontal: true);
        var verticalPattern = PatternShapes.Line(_config.Length, horizontal: false);

        var horizontalCells = horizontalPattern.SelectFree(_target, _payload.Position);
        var verticalCells = verticalPattern.SelectFree(_target, _payload.Position);

        var selected = horizontalCells.Count >= verticalCells.Count ? horizontalCells : verticalCells;

        if (selected.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("No free cells in the line pattern"),
                ActionData = null
            };
        }

        foreach (var cell in selected)
            cell.ToTaken();

        foreach (var cell in selected)
            cell.ToTaken().SetMine();

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.CarpetBomb() {
                TargetPlayer = _target.OwnerId
            }
        };
    }
}
