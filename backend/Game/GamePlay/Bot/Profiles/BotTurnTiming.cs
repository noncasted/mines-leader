using Shared;

namespace Game.GamePlay.Profiles;

public static class BotTurnTiming
{
    public static bool ShouldSkipDelay(GameMatchType type)
    {
        return type == GameMatchType.LastManStandingTurnBased;
    }
}
