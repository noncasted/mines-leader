using Shared;

namespace Game.GamePlay;

/// <summary>
/// Clears a line of random length in the longest available direction, flagging mines and revealing safe cells.
/// </summary>
public class ChaosScout : ICard {
    public ChaosScout(IBoard target, CardConfigOptions.ChaosScout config, CardUsePayload.ChaosScout payload, IPlayer owner, IGameRandom gameRandom) {
        _target = target;
        _config = config;
        _payload = payload;
        _owner = owner;
        _gameRandom = gameRandom;
    }

    private readonly IBoard _target;
    private readonly CardConfigOptions.ChaosScout _config;
    private readonly CardUsePayload.ChaosScout _payload;
    private readonly IPlayer _owner;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use() {
        var actualLength = _gameRandom.Range(_owner, _config.MinLength, _config.MaxLength);

        var horizontalPattern = PatternShapes.Line(actualLength, horizontal: true);
        var verticalPattern = PatternShapes.Line(actualLength, horizontal: false);

        var horizontalCells = horizontalPattern.SelectTaken(_target, _payload.Position);
        var verticalCells = verticalPattern.SelectTaken(_target, _payload.Position);

        var selected = horizontalCells.Count >= verticalCells.Count ? horizontalCells : verticalCells;

        if (selected.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("No cells in the line pattern"),
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
            ActionData = new CardActionSnapshot.ChaosScout() {
                TargetPlayer = _target.OwnerId,
                ActualLength = actualLength
            }
        };
    }
}
