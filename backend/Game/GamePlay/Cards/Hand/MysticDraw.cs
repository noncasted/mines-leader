using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Flips a coin: heads draws cards from the deck, tails returns random cards from hand back to the deck.
/// </summary>
public class MysticDraw : ICard<CardUsePayload.MysticDraw>
{
    public MysticDraw(ICardConfigs configs, IGameRandom gameRandom)
    {
        _configs = configs;
        _gameRandom = gameRandom;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use(CardUseContext context, CardUsePayload.MysticDraw payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.MysticDraw_Normal;
        var isHeads = _gameRandom.FlipCoin(invoker);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId,
            new CardActionSnapshot.MysticDraw()
            {
                TargetPlayer = invoker.User.Id,
                IsHeads = isHeads
            });

        var deckChanged = false;

        if (isHeads)
        {
            for (var i = 0; i < config.WinDraw; i++)
            {
                if (invoker.Deck.Count == 0)
                    break;

                var card = invoker.Deck.DrawCard();
                deckChanged = true;
                var activeCard = invoker.Hand.Add(card);
                snapshot.RecordCardAdd(invoker.User.Id, activeCard.Id, activeCard.Type);
            }
        }
        else
        {
            var toReturn = Math.Min(config.LoseReturn, invoker.Hand.Entries.Count);

            for (var i = 0; i < toReturn; i++)
            {
                var index = _gameRandom.Index(invoker, invoker.Hand.Entries.Count);
                var entry = invoker.Hand.Entries[index];
                invoker.Hand.Remove(entry.Id);
                invoker.Deck.AddCard(entry.Type);
                deckChanged = true;
                snapshot.RecordCardRemove(invoker.User.Id, entry.Id, isStash: true);
            }
        }

        if (deckChanged == true)
            snapshot.RecordDeckUpdate(invoker);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}