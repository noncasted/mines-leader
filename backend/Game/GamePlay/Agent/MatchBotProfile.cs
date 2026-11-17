using Cluster.Configs;
using Game.Session;
using Shared;

namespace Game.GamePlay;

public static class MatchBotProfile
{
    public static BotProfile Resolve(MatchCreateOptions options, IBotConfig config)
    {
        if (options.Fixture?.BotProfile != null)
            return options.Fixture.BotProfile.Value;

        return config.Value.CurrentProfile;
    }

    public static BotProfileConfig ResolveConfig(MatchCreateOptions options, IBotConfig config)
    {
        var profile = Resolve(options, config);

        if (config.Value.Profiles.TryGetValue(profile, out var profileConfig))
            return profileConfig;

        return config.Value.CurrentProfileConfig;
    }
}
