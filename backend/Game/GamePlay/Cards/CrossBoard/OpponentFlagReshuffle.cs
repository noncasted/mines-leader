using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public class OpponentFlagReshuffle : ICard<CardUsePayload.OpponentFlagReshuffle>
{
    public OpponentFlagReshuffle(ICardConfigs configs, IGameContext gameContext, IGameRandom gameRandom)
    {
        _configs = configs;
        _gameContext = gameContext;
        _gameRandom = gameRandom;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use(CardUseContext context, CardUsePayload.OpponentFlagReshuffle payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(context.Snapshot, payload.Position);

        if (board.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells")
            };
        }

        var config = _configs.Value.OpponentFlagReshuffle_Normal;
        var pattern = PatternShapes.Rhombus(config.Size);
        var selected = pattern.SelectTaken(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No free cells in the pattern")
            };
        }

        var flagged = selected.Where(cell => cell.IsFlagged == true).ToList();
        var notFlagged = selected.Where(cell => cell.IsFlagged == false).ToList();
        var flagChanges = new List<(Position Position, bool IsFlagged)>();

        while (flagged.Count != 0 && notFlagged.Count != 0)
        {
            var firstFlagged = flagged.First();
            var randomNotFlaggedIndex = _gameRandom.Index(invoker, notFlagged.Count);
            var randomNotFlagged = notFlagged[randomNotFlaggedIndex];

            firstFlagged.RemoveFlag();
            flagChanges.Add((firstFlagged.Position, false));

            randomNotFlagged.SetFlag();
            flagChanges.Add((randomNotFlagged.Position, true));

            flagged.RemoveAt(0);
            notFlagged.RemoveAt(randomNotFlaggedIndex);
        }

        var minesRecords = board.MinesScanner.Recalculate(snapshot);

        var updatedFreeCells = minesRecords
                               .Select(r => new OpenedCell { Position = r.Position, MinesAround = r.Count })
                               .ToList();

        var selectedPositions = selected.Select(c => c.Position).ToList();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.OpponentFlagReshuffle()
        {
            TargetPlayer = board.OwnerId,
            FlaggedCells = flagChanges.Where(c => c.IsFlagged).Select(c => c.Position).ToList(),
            UnflaggedCells = flagChanges.Where(c => c.IsFlagged == false).Select(c => c.Position).ToList(),
            UpdatedFreeCells = updatedFreeCells,
            TargetCells = selectedPositions
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}