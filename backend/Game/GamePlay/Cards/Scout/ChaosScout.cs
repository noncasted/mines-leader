using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Clears a line of random length in the longest available direction, flagging mines and revealing safe cells.
/// </summary>
public class ChaosScout : ICard<CardUsePayload.ChaosScout>
{
    public ChaosScout(ICardConfigs configs, IGameRandom gameRandom)
    {
        _configs = configs;
        _gameRandom = gameRandom;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.ChaosScout payload)
    {
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        var config = _configs.Value.ChaosScout_Normal;
        var actualLength = _gameRandom.Range(invoker, config.MinLength, config.MaxLength);

        var horizontalPattern = PatternShapes.Line(actualLength, horizontal: true);
        var verticalPattern = PatternShapes.Line(actualLength, horizontal: false);

        var horizontalCells = horizontalPattern.SelectTaken(board, payload.Position);
        var verticalCells = verticalPattern.SelectTaken(board, payload.Position);

        var selected = horizontalCells.Count >= verticalCells.Count ? horizontalCells : verticalCells;

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No cells in the line pattern"),
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
            ActionData = new CardActionSnapshot.ChaosScout()
            {
                TargetPlayer = board.OwnerId,
                ActualLength = actualLength,
                TargetCells = selected.Select(c => c.Position).ToList(),
                ActionCells = CardActionCellsHelper.CollectOpened(board, takenBefore)
            }
        };
    }
}