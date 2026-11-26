namespace Game.Lobbies;

// Same values as SharedSessionRequests.EntryStatus (SessionRequester.ToWire).
[GenerateSerializer]
public enum LobbyJoinStatus
{
    Ok = 0,
    NotFound = 1,
    Full = 2,

    // The lobby of the code has started its match and the match can't be entered now (the lobby is starting it,
    // the game does not let anybody in at the moment).
    Started = 3,
}
