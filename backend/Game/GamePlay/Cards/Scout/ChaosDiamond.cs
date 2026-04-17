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

    public CardUseResult Use(CardUseContext context, CardUsePayload.ChaosDiamond payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
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
                Result = EmptyResponse.Fail("No taken cells in the pattern")
            };
        }

        var toReveal = new List<Position>();
        var flagged = new List<Position>();

        foreach (var cell in selected)
        {
            if (cell.HasMine)
            {
                cell.SetFlag();
                flagged.Add(cell.Position);
            }
            else
            {
                toReveal.Add(cell.Position);
            }
        }

        var revealed = board.Revealer.Reveal(toReveal);

        var openedCells = revealed.Distinct().Select(p => new OpenedCell
        {
            Position = p,
            MinesAround = board.Cells[p].AsFree().MinesAround
        }).ToList();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.ChaosDiamond()
        {
            TargetPlayer = board.OwnerId,
            ActualSize = actualSize,
            TargetCells = selected.Select(c => c.Position).ToList(),
            OpenedCells = openedCells
        });

        foreach (var position in flagged)
            snapshot.RecordFlag(board, position, true);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}