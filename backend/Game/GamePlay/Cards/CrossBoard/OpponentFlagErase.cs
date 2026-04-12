using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class OpponentFlagErase : ICard<CardUsePayload.OpponentFlagErase>
{
    public OpponentFlagErase(ICardConfigs configs, IGameContext gameContext)
    {
        _configs = configs;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.OpponentFlagErase payload)
    {
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells"),
                ActionData = null
            };
        }

        var config = _configs.Value.OpponentFlagErase_Normal;
        var pattern = PatternShapes.Rhombus(config.Size);
        var selected = pattern.SelectTaken(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No free cells in the pattern"),
                ActionData = null
            };
        }

        var flagged = Enumerable.Where<ITakenCell>(selected, cell => cell.IsFlagged == true);

        foreach (var cell in flagged)
            cell.RemoveFlag();

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.OpponentFlagErase()
            {
                TargetPlayer = board.OwnerId
            }
        };
    }
}