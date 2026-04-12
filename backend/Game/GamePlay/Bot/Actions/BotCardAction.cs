using Cluster.Configs;
using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;
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
        ILogger<BotCardAction> logger)
    {
        _cardConfigs = cardConfigs;
        _botContext = botContext;
        _botCardStrategies = botCardStrategies;
        _commandUtils = commandUtils;
        _logger = logger;
    }

    private readonly ICardConfigs _cardConfigs;
    private readonly IBotContext _botContext;
    private readonly IBotCardStrategies _botCardStrategies;
    private readonly IBotCommandUtils _commandUtils;
    private readonly ILogger<BotCardAction> _logger;

    public bool TryExecute(IReadOnlyLifetime lifetime)
    {
        var bot = _botContext.Bot;

        // Оцениваем полезность каждой карты в руке
        var cardsWithUtility = new List<(float, Guid, CardType)>();
        var entries = new List<ActiveCard>(bot.Hand.Entries);
        entries.Shuffle();

        foreach (var activeCard in entries)
        {
            var config = _cardConfigs.Value.All[activeCard.Type];

            // Если нехватает маны - пропускаем
            if (bot.Mana.Current < config.ManaCost)
                continue;

            var strategy = _botCardStrategies.Entries[activeCard.Type];
            var utility = strategy.Evaluate(activeCard.Type);

            if (utility > 0)
                cardsWithUtility.Add((utility, activeCard.Id, activeCard.Type));
        }

        // Если нет карт с положительной полезностью - выходим
        if (cardsWithUtility.Count == 0)
            return false;

        // Выбираем карту с максимальной полезностью
        var (_, selectedCardId, selectedCardType) = cardsWithUtility.OrderByDescending(t => t.Item1).First();
        var cardStrategy = _botCardStrategies.Entries[selectedCardType];

        var cardUsed = cardStrategy.Execute(selectedCardId, selectedCardType);

        _logger.LogInformation("[Game] [Bot] Used card {CardType} with result: {UseResult} ", selectedCardType,
            cardUsed);

        if (cardUsed == false)
            return false;

        bot.Hand.Remove(selectedCardId);
        bot.Stash.Add(selectedCardType);
        bot.Moves.OnUsed();
        bot.Mana.Use(_cardConfigs.Value.All[selectedCardType].ManaCost);
        bot.Actions.OnCardUsed(selectedCardType, _commandUtils.LastUsedPayload!);

        return true;
    }
}