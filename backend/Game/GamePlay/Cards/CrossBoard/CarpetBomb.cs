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

    public CardUseResult Use(IPlayer invoker, CardUsePayload.CarpetBomb payload)
    {
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
                Result = EmptyResponse.Fail("No free cells in the line pattern"),
                ActionData = null
            };
        }

        foreach (var cell in selected)
            cell.ToTaken();

        foreach (var cell in selected)
            cell.ToTaken().SetMine();

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.CarpetBomb()
            {
                TargetPlayer = board.OwnerId,
                TargetCells = selected.Select(c => c.Position).ToList()
            }
        };
    }
}