using Shared;

namespace Game.GamePlay;

/// <summary>
/// Clears a diamond area of random size, flagging mines and revealing safe cells within the pattern.
/// </summary>
public class ChaosDiamond : ICard {
    public ChaosDiamond(IBoard target, CardConfigOptions.ChaosDiamond config, CardUsePayload.ChaosDiamond payload, IPlayer owner, IGameRandom gameRandom) {
        _target = target;
        _config = config;
        _payload = payload;
        _owner = owner;
        _gameRandom = gameRandom;
    }

    private readonly IBoard _target;
    private readonly CardConfigOptions.ChaosDiamond _config;
    private readonly CardUsePayload.ChaosDiamond _payload;
    private readonly IPlayer _owner;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use() {
        var actualSize = _gameRandom.Range(_owner, _config.MinSize, _config.MaxSize);
        var pattern = PatternShapes.Rhombus(actualSize);
        var selected = pattern.SelectTaken(_target, _payload.Position);

        if (selected.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("No taken cells in the pattern"),
                ActionData = null
            };
        }

        foreach (var cell in selected) {
            if (cell.HasMine) {
                cell.SetFlag();
            } else {
                cell.ToFree();
                _target.Revealer.Reveal(cell.Position);
            }
        }

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ChaosDiamond() {
                TargetPlayer = _target.OwnerId,
                ActualSize = actualSize
            }
        };
    }
}
