using Shared;

namespace Game.GamePlay.Profiles;

public static class BotTurnTiming
{
    public static bool IsTurnBased(GameMatchType type)
    {
        return type == GameMatchType.LastManStandingTurnBased;
    }

    /// <summary>
    /// Turn-based has no clock, so leftover Min/MaxRoundTime must not be padded.
    /// Per-action delays still run.
    /// </summary>
    public static bool ShouldSkipRoundPadding(GameMatchType type)
    {
        return IsTurnBased(type);
    }

    public static bool ShouldSkipDelay(GameMatchType type)
    {
        return ShouldSkipRoundPadding(type);
    }
}
