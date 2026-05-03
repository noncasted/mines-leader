using Shared;

namespace Game.GamePlay;

public interface IBotProfileStrategyProvider
{
    IBotProfileStrategy GetStrategy(BotProfile profile);
}

public class BotProfileStrategyProvider : IBotProfileStrategyProvider
{
    private readonly Dictionary<BotProfile, IBotProfileStrategy> _strategies;

    public BotProfileStrategyProvider(
        EasyBotProfile easy,
        MediumBotProfile medium,
        HardBotProfile hard)
    {
        _strategies = new Dictionary<BotProfile, IBotProfileStrategy>
        {
            [BotProfile.Easy] = easy,
            [BotProfile.Medium] = medium,
            [BotProfile.Hard] = hard,
        };
    }

    public IBotProfileStrategy GetStrategy(BotProfile profile)
    {
        if (_strategies.TryGetValue(profile, out var strategy))
            return strategy;

        return _strategies[BotProfile.Medium];
    }
}
