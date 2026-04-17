using Shared;

namespace Game.GamePlay;

public class OpponentBomb : ICard<CardUsePayload.OpponentBomb>
{
    public OpponentBomb(IGameContext gameContext)
    {
        _gameContext = gameContext;
    }

    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.OpponentBomb payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells")
            };
        }

        if (board.Cells.TryGetValue(payload.Position, out var cell) == false)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail($"No cell at position {payload.Position}")
            };
        }

        if (cell.IsTaken() == false)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail($"Cell at position {payload.Position} is not taken")
            };
        }

        var taken = cell.ToTaken();
        var hadMine = taken.HasMine;

        if (hadMine == true)
            taken.Explode();

        var revealed = board.Revealer.Reveal(new[] { cell.Position });

        var openedCells = revealed.Distinct().Select(p => new OpenedCell
        {
            Position = p,
            MinesAround = board.Cells[p].AsFree().MinesAround
        }).ToList();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.OpponentBomb()
        {
            TargetPlayer = board.OwnerId,
            TargetCells = new List<Position> { cell.Position },
            OpenedCells = openedCells
        });

        if (hadMine == true)
        {
            snapshot.RecordExplosion(board, cell.Position);
            opponent.Health.TakeDamage(snapshot, 1);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}