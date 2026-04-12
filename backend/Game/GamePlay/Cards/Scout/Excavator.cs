using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Targets a cross-shaped area: flags mines and reveals safe cells within the pattern.
/// </summary>
public class Excavator : ICard<CardUsePayload.Excavator>
{
    public Excavator(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Excavator payload)
    {
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        var pattern = PatternShapes.Cross(_configs.Value.Excavator_Normal.Size);
        var selected = pattern.SelectTaken(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No taken cells in the cross pattern"),
                ActionData = null
            };
        }

        foreach (var cell in selected)
        {
            if (cell.HasMine)
            {
                cell.SetFlag();
            }
            else
            {
                cell.ToFree();
                board.Revealer.Reveal(cell.Position);
            }
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Excavator()
            {
                TargetPlayer = board.OwnerId
            }
        };
    }
}