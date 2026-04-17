using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Plants mines in a cross-shaped pattern on free cells of the opponent's field.
/// </summary>
public class MineCluster : ICard<CardUsePayload.MineCluster>
{
    public MineCluster(ICardConfigs configs, IGameContext gameContext)
    {
        _configs = configs;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.MineCluster payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        var config = _configs.Value.MineCluster_Normal;
        var pattern = PatternShapes.Cross(config.Size);
        var selected = pattern.SelectFree(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No free cells in the cross pattern")
            };
        }

        foreach (var cell in selected)
            cell.ToTaken().SetMine();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.MineCluster()
        {
            TargetPlayer = board.OwnerId,
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