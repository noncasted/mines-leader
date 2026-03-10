using Shared;

namespace Game.GamePlay;

public class OpponentBomb : ICard
{
    public OpponentBomb(
        IPlayer opponent,
        IBoard target,
        CardUsePayload.OpponentBomb payload)
    {
        _opponent = opponent;
        _target = target;
        _payload = payload;
    }

    private readonly IPlayer _opponent;
    private readonly IBoard _target;
    private readonly CardUsePayload.OpponentBomb _payload;

    public CardUseResult Use()
    {
        if (_target.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells"),
                ActionData = null
            };
        }
        
        if (_target.Cells.TryGetValue(_payload.Position, out var cell) == false)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail($"No cell at position {_payload.Position}"),
                ActionData = null
            };
        }

        if (cell.IsTaken() == false)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail($"Cell at position {_payload.Position} is not taken"),
                ActionData = null
            };
        }

        var taken = cell.ToTaken();

        if (taken.HasMine == true)
        {
            taken.Explode();
            _opponent.Health.TakeDamage(1);
        }
        else
        {
            taken.ToFree();
        }

        _target.OnUpdated();
        _target.Revealer.Reveal(cell.Position);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.OpponentBomb()
            {
                TargetPlayer = _target.OwnerId
            }
        };
    }
}