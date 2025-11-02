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

    public EmptyResponse Use()
    {
        if (_target.Cells.TryGetValue(_payload.Position, out var cell) == false)
            return EmptyResponse.Fail($"No cell at position {_payload.Position}");

        if (cell.IsTaken() == false)
            return EmptyResponse.Fail($"Cell at position {_payload.Position} is not taken");

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

        _target.Revealer.Reveal(cell.Position);

        return EmptyResponse.Ok;
    }
}