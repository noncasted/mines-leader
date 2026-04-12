using Shared;
using Cluster.Configs;

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

    public CardUseResult Use(IPlayer invoker, CardUsePayload.OpponentFlagReshuffle payload)
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

        var config = _configs.Value.OpponentFlagReshuffle_Normal;
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

        var flagged = Enumerable.Where<ITakenCell>(selected, cell => cell.IsFlagged == true).ToList();
        var notFlagged = Enumerable.Where<ITakenCell>(selected, cell => cell.IsFlagged == false).ToList();

        while (flagged.Count != 0 && notFlagged.Count != 0)
        {
            var firstFlagged = flagged.First();
            var randomNotFlaggedIndex = _gameRandom.Index(invoker, notFlagged.Count);
            var randomNotFlagged = notFlagged[randomNotFlaggedIndex];

            firstFlagged.RemoveFlag();
            randomNotFlagged.SetFlag();
            flagged.RemoveAt(0);
            notFlagged.RemoveAt(randomNotFlaggedIndex);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.OpponentFlagReshuffle()
            {
                TargetPlayer = board.OwnerId
            }
        };
    }
}