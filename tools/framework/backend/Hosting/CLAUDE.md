# Hosting

`backend/Hosting`. A game server calls `AddSessionHosting(LobbyOptions)`: the sessions collection
(`ISessionsCollection` of `Game.Hosting.Collection`, feeds `LiveSessionsData`), the lobby sessions (`ILobbySessions`: create, join by a code,
the lobby of the late players of a running match), the leases of the lobby codes (`ILobbyCodeLeases`,
`ILobbyListings`) and the handlers of the meta requests (`SessionEndpoints`, `SessionPayloads` over the message
pipes). The game registers what builds its sessions: `ILobbyFactory` (a lobby session from `LobbyCreation`),
`ILobbyMatches` (the match a lobby starts, the admission of a late player), `IKnownUsersMatches` (a solo match),
`ISessionSearch` (the public match of the search in a queue of the game: an int, 0 by default, its meaning is the
game's) and `ILobbyEntryPolicy` (when a running match lets a late player in; `LobbyEntryPolicy.Always` when
always). The container of a lobby session adds `AddLobbyService(LobbySessionInfo)`
(or `AddLobbyService<T>` with a derived service: own properties from `SharedLobbyService.GamePropertyFirst`, the
admission of the late players in `OnLatePlayersStarted` / `Admit` / `ClearGame` / `MarkRunOver`), the container of
a match started by a lobby `AddMatchListingService(MatchListingInfo)`. The meta calls `AddSessionRequester`:
`ISessionRequester` (search, solo, lobby create / join / match join, tickets) and `IOpenLobbiesWatcher`; the
commands of the meta socket that call them stay in the game. The lobby codes live in the registry grain of the
deploy (`backend/Cluster/Lobbies`, `ILobbyCodes`). The protocol: `SharedSessionRequests` (`Search`, `CreateSolo`,
`Result`, `TicketRequest`) and `SharedLobbies` (`CreateLobby`, `JoinLobby`, `JoinMatch`, `Subscribe`, `List`),
tags `NetworkContextTags.Session*` / `Lobb*` (150-155, 180-184), listed by the project in its union.
