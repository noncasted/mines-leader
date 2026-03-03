using Common.Reactive;
using Cluster.Configs;
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
        IBotCardStrategies botCardStrategies)
    {
        _cardConfigs = cardConfigs;
        _botContext = botContext;
        _botCardStrategies = botCardStrategies;
    }

    private readonly ICardConfigs _cardConfigs;
    private readonly IBotContext _botContext;
    private readonly IBotCardStrategies _botCardStrategies;
    private readonly IBotCommandUtils _commandUtils;

    public bool TryExecute(IReadOnlyLifetime lifetime)
    {
        var bot = _botContext.Bot;

        // Оцениваем полезность каждой карты в руке
        var cardsWithUtility = new List<(float, CardType)>();

        foreach (var cardType in bot.Hand.Entries)
        {
            var config = _cardConfigs.Value.All[cardType];

            // Если нехватает маны - пропускаем
            if (bot.Mana.Current < config.ManaCost)
                continue;

            var strategy = _botCardStrategies.Entries[cardType];
            var utility = strategy.Evaluate(cardType);

            if (utility > 0)
                cardsWithUtility.Add((utility, cardType));
        }

        // Если нет карт с положительной полезностью - выходим
        if (cardsWithUtility.Count == 0)
            return false;

        // Выбираем карту с максимальной полезностью
        var selectedCard = cardsWithUtility.OrderBy(t => t.Item1).First().Item2;
        var cardStrategy = _botCardStrategies.Entries[selectedCard];

        var cardUsed = cardStrategy.Execute(selectedCard);

        if (cardUsed == false)
            return false;

        bot.Board.OnUpdated();
        _botContext.Opponent.Board.OnUpdated();

        bot.Hand.Remove(selectedCard);
        bot.Stash.Add(selectedCard);
        bot.Moves.OnUsed();
        bot.Board.OnUpdated();
        bot.Mana.Use(_cardConfigs.Value.All[selectedCard].ManaCost);

        return true;
    }
}