using Shared;

namespace Game.GamePlay;

/// <summary>
/// Steals a random card from the opponent's hand and adds it to the owner's hand.
/// </summary>
public class CardThief : ICard<CardUsePayload.CardThief>
{
    public CardThief(IGameContext gameContext, IGameRandom gameRandom)
    {
        _gameContext = gameContext;
        _gameRandom = gameRandom;
    }

    private readonly IGameContext _gameContext;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use(CardUseContext context, CardUsePayload.CardThief payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var handEntries = opponent.Hand.Entries.ToList();

        if (handEntries.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Opponent has no cards in hand")
            };
        }

        var index = _gameRandom.Index(invoker, handEntries.Count);
        var entry = handEntries[index];

        snapshot.RecordCardUse(invoker.User.Id, context.CardId,
            new CardActionSnapshot.CardThief()
            {
                TargetPlayer = opponent.User.Id,
                StolenCard = entry.Type
            });

        opponent.Hand.Remove(entry.Id);
        snapshot.RecordCardRemove(opponent.User.Id, entry.Id);

        var activeCard = invoker.Hand.Add(entry.Type);
        snapshot.RecordCardAdd(invoker.User.Id, activeCard.Id, activeCard.Type);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}