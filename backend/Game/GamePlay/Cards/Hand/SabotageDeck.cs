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

    public CardUseResult Use(CardUseContext context, CardUsePayload.SabotageDeck payload)
    {
        var invoker = context.Invoker;
        var opponent = _gameContext.GetOpponent(invoker);
        opponent.Deck.AddCard(CardType.Dud);
        opponent.Deck.Shuffle();

        var snapshot = context.Snapshot;

        snapshot.RecordCardUse(context.Invoker.User.Id, context.CardId, new CardActionSnapshot.SabotageDeck()
        {
            TargetPlayer = opponent.User.Id
        });
        snapshot.RecordDeckUpdate(opponent);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}