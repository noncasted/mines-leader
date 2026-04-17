using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Plants mines along the longest available line on free cells of the opponent's field.
/// </summary>
public class CarpetBomb : ICard<CardUsePayload.CarpetBomb>
{
    public CarpetBomb(ICardConfigs configs, IGameContext gameContext)
    {
        _configs = configs;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.CarpetBomb payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        var config = _configs.Value.CarpetBomb_Normal;
        var horizontalPattern = PatternShapes.Line(config.Length, horizontal: true);
        var verticalPattern = PatternShapes.Line(config.Length, horizontal: false);

        var horizontalCells = horizontalPattern.SelectFree(board, payload.Position);
        var verticalCells = verticalPattern.SelectFree(board, payload.Position);

        var selected = horizontalCells.Count >= verticalCells.Count ? horizontalCells : verticalCells;

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No free cells in the line pattern")
            };
        }

        foreach (var cell in selected)
            cell.ToTaken().SetMine();

        var takenPositions = selected.Select(c => c.Position).ToList();
        var minesRecords = board.MinesScanner.Recalculate(snapshot);
        var updatedFreeCells = minesRecords
            .Select(r => new OpenedCell { Position = r.Position, MinesAround = r.Count })
            .ToList();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.CarpetBomb()
        {
            TargetPlayer = board.OwnerId,
            TargetCells = takenPositions,
            TakenCells = takenPositions,
            UpdatedFreeCells = updatedFreeCells
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}