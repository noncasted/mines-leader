using Newtonsoft.Json;

namespace Cluster.Lobbies;

[GenerateSerializer]
public class LobbyRegistryEntry
{
    [Id(0)] public Guid ServerId { get; init; }
    [Id(1)] public Guid SessionId { get; init; }

    // The match the lobby has started; empty while the lobby has not started. From that moment the code is
    // held for the match: a join by it leads to the lobby of the late players of the match.
    [Id(2)] [JsonProperty] public Guid MatchSessionId { get; private set; }

    // What the "Connect" window shows of the lobby (or of its match), reported by the game server.
    [Id(3)] [JsonProperty] public LobbyListing Listing { get; private set; } = new();

    public void MarkStarted(Guid matchSessionId)
    {
        MatchSessionId = matchSessionId;
    }

    public void UpdateListing(LobbyListing listing)
    {
        Listing = listing;
    }
}
