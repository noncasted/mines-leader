using Shared;

namespace Game.GamePlay;

/// <summary>
/// Injects a useless Dud card into the opponent's deck and shuffles it.
/// </summary>
public class SabotageDeck : ICard
{
    public SabotageDeck(IPlayer opponent)
    {
        _opponent = opponent;
    }

    private readonly IPlayer _opponent;

    public CardUseResult Use()
    {
        _opponent.Deck.AddCard(CardType.Dud);
        _opponent.Deck.Shuffle();

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.SabotageDeck()
            {
                TargetPlayer = _opponent.User.Id
            }
        };
    }
}
