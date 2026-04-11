using Shared;

namespace Game.GamePlay;

/// <summary>
/// Reveals a random number of hidden mines on the owner's field as temporary highlights without flagging them.
/// </summary>
public class FortuneCookie : ICard
{
    public FortuneCookie(
        IPlayer owner,
        IBoard board,
        CardConfigOptions.FortuneCookie config,
        IGameRandom gameRandom)
    {
        _owner = owner;
        _board = board;
        _config = config;
        _gameRandom = gameRandom;
    }

    private readonly IPlayer _owner;
    private readonly IBoard _board;
    private readonly CardConfigOptions.FortuneCookie _config;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use()
    {
        var mineCells = _board.Cells.Values
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

        var count = _gameRandom.Range(_owner, _config.MinMines, _config.MaxMines);
        count = Math.Min(count, mineCells.Count);

        var revealed = new List<Position>(count);

        for (var i = 0; i < count; i++)
        {
            var index = _gameRandom.Index(_owner, mineCells.Count);
            revealed.Add(mineCells[index].Position);
            mineCells.RemoveAt(index);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.FortuneCookie()
            {
                TargetPlayer = _owner.User.Id,
                RevealedMines = revealed
            }
        };
    }
}
