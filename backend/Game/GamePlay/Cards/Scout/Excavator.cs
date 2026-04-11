using Shared;

namespace Game.GamePlay;

/// <summary>
/// Targets a cross-shaped area: flags mines and reveals safe cells within the pattern.
/// </summary>
public class Excavator : ICard {
    public Excavator(IBoard target, CardConfigOptions.Excavator config, CardUsePayload.Excavator payload) {
        _target = target;
        _config = config;
        _payload = payload;
    }

    private readonly IBoard _target;
    private readonly CardConfigOptions.Excavator _config;
    private readonly CardUsePayload.Excavator _payload;

    public CardUseResult Use() {
        var pattern = PatternShapes.Cross(_config.Size);
        var selected = pattern.SelectTaken(_target, _payload.Position);

        if (selected.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("No taken cells in the cross pattern"),
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
            ActionData = new CardActionSnapshot.Excavator() {
                TargetPlayer = _target.OwnerId
            }
        };
    }
}
