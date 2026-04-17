using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Flips a coin: heads draws cards and grants temporary mana, tails discards random cards from hand.
/// </summary>
public class GamblersRuin : ICard<CardUsePayload.GamblersRuin>
{
    public GamblersRuin(ICardConfigs configs, IRoundActionService roundActionService, IGameRandom gameRandom)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameRandom = gameRandom;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use(CardUseContext context, CardUsePayload.GamblersRuin payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.GamblersRuin_Normal;
        var isHeads = _gameRandom.FlipCoin(invoker);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId,
            new CardActionSnapshot.GamblersRuin()
            {
                TargetPlayer = invoker.User.Id,
                IsHeads = isHeads
            });

        if (isHeads)
        {
            var drawn = 0;

            for (var i = 0; i < config.WinDraw; i++)
            {
                if (invoker.Deck.Count == 0)
                    break;

                var card = invoker.Deck.DrawCard();
                var activeCard = invoker.Hand.Add(card);
                snapshot.RecordCardAdd(invoker.User.Id, activeCard.Id, activeCard.Type);
                drawn++;
            }

            if (drawn > 0)
                snapshot.RecordDeckUpdate(invoker);

            var manaGain = config.WinMana;
            invoker.Modifiers.Inc(snapshot, PlayerModifier.AdditionalMana, manaGain);
            invoker.Mana.SetCurrent(snapshot, invoker.Mana.Current + manaGain);

            _roundActionService.Schedule(new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMana, manaGain),
                1);
        }
        else
        {
            var candidates = invoker.Hand.Entries
                                    .Where(c => c.Id != context.CardId)
                                    .ToList();

            var toDiscard = Math.Min(config.LoseDiscard, candidates.Count);
            var stashChanged = false;

            for (var i = 0; i < toDiscard; i++)
            {
                var index = _gameRandom.Index(invoker, candidates.Count);
                var entry = candidates[index];
                invoker.Hand.Remove(entry.Id);
                invoker.Stash.Add(entry.Type);
                stashChanged = true;
                snapshot.RecordCardRemove(invoker.User.Id, entry.Id);
                candidates.RemoveAt(index);
            }

            if (stashChanged == true)
                snapshot.RecordStashUpdate(invoker);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}