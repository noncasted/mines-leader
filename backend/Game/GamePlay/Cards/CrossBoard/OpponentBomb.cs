using Shared;

namespace Game.GamePlay;

public class OpponentBomb : ICard<CardUsePayload.OpponentBomb>
{
    public OpponentBomb(IGameContext gameContext)
    {
        _gameContext = gameContext;
    }

    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.OpponentBomb payload)
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

        if (board.Cells.TryGetValue(payload.Position, out var cell) == false)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail($"No cell at position {payload.Position}"),
                ActionData = null
            };
        }

        if (cell.IsTaken() == false)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail($"Cell at position {payload.Position} is not taken"),
                ActionData = null
            };
        }

        var taken = cell.ToTaken();

        if (taken.HasMine == true)
        {
            taken.Explode();
            opponent.Health.TakeDamage(1);
        }

        taken.ToFree();
        board.OnUpdated();
        board.Revealer.Reveal(cell.Position);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.OpponentBomb()
            {
                TargetPlayer = board.OwnerId
            }
        };
    }
}