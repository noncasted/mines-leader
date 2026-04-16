using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class ErosionDozer : ICard<CardUsePayload.ErosionDozer>
{
    public ErosionDozer(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.ErosionDozer payload)
    {
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        var size = _configs.Value.ErosionDozer_Normal.Size;

        var selected = board.GetClosedShape(payload.Position);
        var ordered = selected.OrderBy(t => t.Position.DistanceTo(payload.Position));

        var limited = ordered.Take(size).ToList();

        if (limited.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No taken cells in the pattern"),
                ActionData = null
            };
        }

        var takenBefore = CardActionCellsHelper.CaptureTaken(board);

        foreach (var cell in limited)
            cell.ToFree();

        foreach (var cell in limited)
            board.Revealer.Reveal(cell.Position);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ErosionDozer()
            {
                TargetPlayer = board.OwnerId,
                TargetCells = limited.Select(c => c.Position).ToList(),
                ActionCells = CardActionCellsHelper.CollectOpened(board, takenBefore)
            }
        };
    }
}