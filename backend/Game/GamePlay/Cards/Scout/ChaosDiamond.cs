using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Clears a diamond area of random size, flagging mines and revealing safe cells within the pattern.
/// </summary>
public class ChaosDiamond : ICard<CardUsePayload.ChaosDiamond>
{
    public ChaosDiamond(ICardConfigs configs, IGameRandom gameRandom)
    {
        _configs = configs;
        _gameRandom = gameRandom;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.ChaosDiamond payload)
    {
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        var config = _configs.Value.ChaosDiamond_Normal;
        var actualSize = _gameRandom.Range(invoker, config.MinSize, config.MaxSize);
        var pattern = PatternShapes.Rhombus(actualSize);
        var selected = pattern.SelectTaken(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No taken cells in the pattern"),
                ActionData = null
            };
        }

        var takenBefore = CardActionCellsHelper.CaptureTaken(board);

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
            ActionData = new CardActionSnapshot.ChaosDiamond()
            {
                TargetPlayer = board.OwnerId,
                ActualSize = actualSize,
                TargetCells = selected.Select(c => c.Position).ToList(),
                ActionCells = CardActionCellsHelper.CollectOpened(board, takenBefore)
            }
        };
    }
}