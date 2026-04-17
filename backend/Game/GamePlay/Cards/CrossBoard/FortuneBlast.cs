using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Plants mines in a diamond area of random size on free cells of the opponent's field.
/// </summary>
public class FortuneBlast : ICard<CardUsePayload.FortuneBlast>
{
    public FortuneBlast(ICardConfigs configs, IGameContext gameContext, IGameRandom gameRandom)
    {
        _configs = configs;
        _gameContext = gameContext;
        _gameRandom = gameRandom;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use(CardUseContext context, CardUsePayload.FortuneBlast payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        var config = _configs.Value.FortuneBlast_Normal;
        var actualSize = _gameRandom.Range(invoker, config.MinSize, config.MaxSize);
        var pattern = PatternShapes.Rhombus(actualSize);
        var selected = pattern.SelectFree(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No free cells in the pattern")
            };
        }

        foreach (var cell in selected)
            cell.ToTaken().SetMine();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.FortuneBlast()
        {
            TargetPlayer = board.OwnerId,
            ActualSize = actualSize,
            TargetCells = selected.Select(c => c.Position).ToList()
        });

        foreach (var cell in selected)
            snapshot.RecordCellTaken(board, cell.Position);

        snapshot.RecordMines(board, board.MinesScanner.Recalculate(snapshot));

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}