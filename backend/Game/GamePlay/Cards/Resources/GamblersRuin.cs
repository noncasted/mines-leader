using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Flips a coin: heads draws cards and grants temporary mana, tails discards random cards from hand.
/// </summary>
public class GamblersRuin : ICard<CardUsePayload.GamblersRuin>
{
    public GamblersRuin(
        ICardConfigs configs,
        IRoundActionService roundActionService,
        IGameRandom gameRandom,
        IMoveSnapshotAccessor snapshotAccessor)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameRandom = gameRandom;
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameRandom _gameRandom;
    private readonly IMoveSnapshotAccessor _snapshotAccessor;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.GamblersRuin payload)
    {
        var config = _configs.Value.GamblersRuin_Normal;
        var isHeads = _gameRandom.FlipCoin(invoker);

        _snapshotAccessor.Snapshot.RecordCardUse(invoker.User.Id, _snapshotAccessor.CardId,
            new CardActionSnapshot.GamblersRuin()
            {
                TargetPlayer = invoker.User.Id,
                IsHeads = isHeads
            });

        if (isHeads)
        {
            for (var i = 0; i < config.WinDraw; i++)
            {
                if (invoker.Deck.Count == 0)
                    break;

                var card = invoker.Deck.DrawCard();
                var activeCard = invoker.Hand.Add(card);
                _snapshotAccessor.Snapshot.RecordCardAdd(invoker.User.Id, activeCard.Id, activeCard.Type);
            }

            var manaGain = config.WinMana;
            invoker.Modifiers.Inc(PlayerModifier.AdditionalMana, manaGain);
            invoker.Mana.SetCurrent(invoker.Mana.Current + manaGain);

            _roundActionService.Schedule(new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMana, manaGain),
                1);
        }
        else
        {
            var candidates = invoker.Hand.Entries
                .Where(c => c.Id != _snapshotAccessor.CardId)
                .ToList();

            var toDiscard = Math.Min(config.LoseDiscard, candidates.Count);

            for (var i = 0; i < toDiscard; i++)
            {
                var index = _gameRandom.Index(invoker, candidates.Count);
                var entry = candidates[index];
                invoker.Hand.Remove(entry.Id);
                invoker.Stash.Add(entry.Type);
                _snapshotAccessor.Snapshot.RecordCardRemove(invoker.User.Id, entry.Id);
                candidates.RemoveAt(index);
            }
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = null
        };
    }
}