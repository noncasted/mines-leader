using Shared;

namespace Game.GamePlay;

/// <summary>
/// Injects a useless Dud card into the opponent's deck and shuffles it.
/// </summary>
public class SabotageDeck : ICard<CardUsePayload.SabotageDeck>
{
    public SabotageDeck(IGameContext gameContext)
    {
        _gameContext = gameContext;
    }

    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.SabotageDeck payload)
    {
        var opponent = _gameContext.GetOpponent(invoker);
        opponent.Deck.AddCard(CardType.Dud);
        opponent.Deck.Shuffle();

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.SabotageDeck()
            {
                TargetPlayer = opponent.User.Id
            }
        };
    }
}
