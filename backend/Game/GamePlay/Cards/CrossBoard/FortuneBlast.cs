using Shared;

namespace Game.GamePlay;

/// <summary>
/// Plants mines in a diamond area of random size on free cells of the opponent's field.
/// </summary>
public class FortuneBlast : ICard {
    public FortuneBlast(IPlayer owner, IBoard target, CardConfigOptions.FortuneBlast config, CardUsePayload.FortuneBlast payload, IGameRandom gameRandom) {
        _owner = owner;
        _target = target;
        _config = config;
        _payload = payload;
        _gameRandom = gameRandom;
    }

    private readonly IPlayer _owner;
    private readonly IBoard _target;
    private readonly CardConfigOptions.FortuneBlast _config;
    private readonly CardUsePayload.FortuneBlast _payload;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use() {
        var actualSize = _gameRandom.Range(_owner, _config.MinSize, _config.MaxSize);
        var pattern = PatternShapes.Rhombus(actualSize);
        var selected = pattern.SelectFree(_target, _payload.Position);

        if (selected.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("No free cells in the pattern"),
                ActionData = null
            };
        }

        foreach (var cell in selected)
            cell.ToTaken();

        foreach (var cell in selected)
            cell.ToTaken().SetMine();

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.FortuneBlast() {
                TargetPlayer = _target.OwnerId,
                ActualSize = actualSize
            }
        };
    }
}
