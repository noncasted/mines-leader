using Shared;

namespace Game.GamePlay;

public interface IBotCardStrategy
{
    IReadOnlyList<CardType> TargetCards { get; }

    float Evaluate(CardType type);
    bool Execute(Guid cardId, CardType cardType);
}

public interface IBotCardStrategies
{
    IReadOnlyDictionary<CardType, IBotCardStrategy> Entries { get; }
}

public class BotCardStrategies : IBotCardStrategies
{
    public BotCardStrategies(IEnumerable<IBotCardStrategy> strategies)
    {
        var entries = new Dictionary<CardType, IBotCardStrategy>();
        Entries = entries;

        foreach (var strategy in strategies)
        {
            foreach (var cardType in strategy.TargetCards)
                entries.Add(cardType, strategy);
        }
    }

    public IReadOnlyDictionary<CardType, IBotCardStrategy> Entries { get; }
}