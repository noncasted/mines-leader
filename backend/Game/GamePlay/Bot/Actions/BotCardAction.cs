using Cluster.Configs;
using Common.Reactive;
using Game.Session;
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
        ILogger<BotCardAction> logger,
        ISessionEntities entities,
        ISessionUsers users)
    {
        _cardConfigs = cardConfigs;
        _botContext = botContext;
        _botCardStrategies = botCardStrategies;
        _logger = logger;
        _entities = entities;
        _users = users;
    }

    private readonly ICardConfigs _cardConfigs;
    private readonly IBotContext _botContext;
    private readonly IBotCardStrategies _botCardStrategies;
    private readonly ISessionEntities _entities;
    private readonly ISessionUsers _users;
    private readonly ILogger<BotCardAction> _logger;

    public bool TryExecute(IReadOnlyLifetime lifetime)
    {
        var bot = _botContext.Bot;

        // Оцениваем полезность каждой карты в руке
        var cardsWithUtility = new List<(float, CardType)>();
        var entries = new List<CardType>(bot.Hand.Entries).Shuffle();

        foreach (var cardType in entries)
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
        var selectedCard = cardsWithUtility.OrderByDescending(t => t.Item1).First().Item2;
        var cardStrategy = _botCardStrategies.Entries[selectedCard];

        var cardUsed = cardStrategy.Execute(selectedCard);
        _logger.LogInformation("[Game] [Bot] Used card {CardType} with result: {UseResult} ", selectedCard, cardUsed);

        if (cardUsed == false)
            return false;

        bot.Hand.Remove(selectedCard);
        bot.Stash.Add(selectedCard);
        bot.Moves.OnUsed();
        bot.Mana.Use(_cardConfigs.Value.All[selectedCard].ManaCost);

        var cardEntity = GetCardEntity();

        if (cardEntity != null)
        {
            cardEntity.Destroy();

            var update = new SharedSessionEntity.DestroyUpdate()
            {
                EntityId = cardEntity.Id
            };

            _users.SendAllExceptSelf(bot.User, update);
        }
        
        return true;

        IEntity? GetCardEntity()
        {
            foreach (var (_, entity) in _entities.Entries)
            {
                if (entity.Owner != bot.User)
                    continue;

                if (entity.Payload is not CardCreatePayload cardPayload)
                    continue;

                if (cardPayload.Type != selectedCard)
                    continue;

                return entity;
            }

            return null;
        }
    }
}