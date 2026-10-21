using Shared;

namespace Game.Session;

// The game options of a session, registered in its container by the session factory.
public class LobbyCreateOptions
{
}

public class MatchCreateOptions
{
    public required GameMatchType Type { get; init; }
    public AgentMatchFixture? Fixture { get; init; }
}
