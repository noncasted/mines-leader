using Shared;

namespace Game.GamePlay;

/// <summary>
/// Copies the last card played by the opponent and applies its effect for the owner.
/// </summary>
public class MirrorMatch : ICard
{
    public MirrorMatch(
        IPlayer owner,
        IPlayer opponent,
        ICardFactory cardFactory,
        MoveSnapshot snapshot)
    {
        _owner = owner;
        _opponent = opponent;
        _cardFactory = cardFactory;
        _snapshot = snapshot;
    }

    private readonly IPlayer _owner;
    private readonly IPlayer _opponent;
    private readonly ICardFactory _cardFactory;
    private readonly MoveSnapshot _snapshot;

    public CardUseResult Use()
    {
        // TODO: Full implementation requires LastUsedCard tracking on IPlayer.
        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.MirrorMatch()
            {
                TargetPlayer = _owner.User.Id,
                CopiedCard = CardType.MirrorMatch
            }
        };
    }
}
