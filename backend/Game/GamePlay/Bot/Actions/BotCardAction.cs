using Cluster.Configs;
using Common.Extensions;
using Common.Reactive;
using Game.Session;
using Shared;

namespace Game.GamePlay;

public interface IBotCardAction
{
    bool TryExecute(IReadOnlyLifetime lifetime);
}

public class BotCardAction : IBotCardAction
{
    public BotCardAction(
        ICardConfigs cardConfigs,
        IBotContext botContext,
        IBotCardStrategies botCardStrategies,
        IBotCommandUtils commandUtils,
        ISessionLogger sessionLogger)
    {
        _cardConfigs = cardConfigs;
        _botContext = botContext;
        _botCardStrategies = botCardStrategies;
        _commandUtils = commandUtils;
        _sessionLogger = sessionLogger;
    }

    private readonly ICardConfigs _cardConfigs;
    private readonly IBotContext _botContext;
    private readonly IBotCardStrategies _botCardStrategies;
    private readonly IBotCommandUtils _commandUtils;
    private readonly ISessionLogger _sessionLogger;

    public bool TryExecute(IReadOnlyLifetime lifetime)
    {
        var bot = _botContext.Bot;
        var currentMana = bot.Mana.Current;

        var entries = new List<ActiveCard>(bot.Hand.Entries);
        entries.Shuffle();

        var cardsWithUtility = new List<(float utility, Guid id, CardType type)>();

        var skippedNoMana = new List<(CardType type, int cost)>();
        var skippedNoStrategy = new List<CardType>();
        var skippedZeroUtility = new List<(CardType type, float utility)>();

        foreach (var activeCard in entries)
        {
            var config = _cardConfigs.Value.All[activeCard.Type];

            if (currentMana < config.ManaCost)
            {
                skippedNoMana.Add((activeCard.Type, config.ManaCost));
                continue;
            }

            if (_botCardStrategies.Entries.TryGetValue(activeCard.Type, out var strategy) == false)
            {
                skippedNoStrategy.Add(activeCard.Type);
                continue;
            }

            var utility = strategy.Evaluate(activeCard.Type);

            if (utility <= 0)
            {
                skippedZeroUtility.Add((activeCard.Type, utility));
                continue;
            }

            var manaCost = config.ManaCost;
            var manaBonus = (1f - manaCost / 6f) * 1.5f;
            var effectiveUtility = utility + manaBonus;

            cardsWithUtility.Add((effectiveUtility, activeCard.Id, activeCard.Type));
        }

        var evaluations = cardsWithUtility.Select(c => {
            var cost = _cardConfigs.Value.All[c.type].ManaCost;
            return $"{c.type}={c.utility:F1}(cost {cost})";
        });
        var manaSkips = skippedNoMana.Select(c => $"{c.type}(need {c.cost})");
        var utilitySkips = skippedZeroUtility.Select(c => $"{c.type}={c.utility:F1}");

        _sessionLogger.LogBotAction("CardEval",
            $"Mana={currentMana} | Candidates=[{string.Join(", ", evaluations)}] | NoMana=[{string.Join(", ", manaSkips)}] | ZeroUtility=[{string.Join(", ", utilitySkips)}]" +
            (skippedNoStrategy.Count > 0 ? $" | NoStrategy=[{string.Join(", ", skippedNoStrategy)}]" : ""));

        if (cardsWithUtility.Count == 0)
            return false;

        foreach (var (utility, cardId, cardType) in cardsWithUtility.OrderByDescending(t => t.utility))
        {
            var cardStrategy = _botCardStrategies.Entries[cardType];
            var manaCost = _cardConfigs.Value.All[cardType].ManaCost;

            var cardUsed = cardStrategy.Execute(cardId, cardType);

            if (cardUsed == false)
            {
                _sessionLogger.LogBotAction("Card",
                    $"FAILED to execute {cardType} | Utility={utility:F1} ManaCost={manaCost}");
                continue;
            }

            _commandUtils.WithSnapshot(snapshot => {
                bot.Hand.Remove(cardId);
                snapshot.RecordCardRemove(bot.User.Id, cardId);

                bot.Mana.Use(snapshot, manaCost);
                bot.Moves.OnUsed(snapshot);

                bot.Stash.Add(cardType);
                snapshot.RecordCardAdd(bot.User.Id, cardId, cardType, isStash: true);
                snapshot.RecordStashUpdate(bot);

                bot.Actions.OnCardUsed(cardType, _commandUtils.LastUsedPayload!);
            });

            _sessionLogger.LogBotAction("Card",
                $"Used {cardType} | Utility={utility:F1} ManaCost={manaCost} ManaLeft={bot.Mana.Current} MovesLeft={bot.Moves.Left}");

            return true;
        }

        _sessionLogger.LogBotAction("Card", "All candidates failed to execute");
        return false;
    }
}
