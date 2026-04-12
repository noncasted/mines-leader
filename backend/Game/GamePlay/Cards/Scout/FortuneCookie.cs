using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Reveals a random number of hidden mines on the owner's field as temporary highlights without flagging them.
/// </summary>
public class FortuneCookie : ICard<CardUsePayload.FortuneCookie>
{
    public FortuneCookie(ICardConfigs configs, IGameRandom gameRandom)
    {
        _configs = configs;
        _gameRandom = gameRandom;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.FortuneCookie payload)
    {
        var board = invoker.Board;

        var mineCells = board.Cells.Values
                             .Where(c => c.Status == CellStatus.Taken)
                             .Select(c => c.ToTaken())
                             .Where(c => c.HasMine)
                             .ToList();

        if (mineCells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No hidden mines on board"),
                ActionData = null
            };
        }

        var config = _configs.Value.FortuneCookie_Normal;
        var count = _gameRandom.Range(invoker, config.MinMines, config.MaxMines);
        count = Math.Min(count, mineCells.Count);

        var revealed = new List<Position>(count);

        for (var i = 0; i < count; i++)
        {
            var index = _gameRandom.Index(invoker, mineCells.Count);
            revealed.Add(mineCells[index].Position);
            mineCells.RemoveAt(index);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.FortuneCookie()
            {
                TargetPlayer = invoker.User.Id,
                RevealedMines = revealed
            }
        };
    }
}