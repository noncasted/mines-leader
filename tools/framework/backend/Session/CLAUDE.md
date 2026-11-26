# Sessions

Shared types in `shared/Session` (auth, entities, objects, players, services, latency, entity ids:
`SharedEntityIds`, range 0 issued by the backend). Backend `backend/Session`: `AddSessionServices(data, options)`
builds the container of one session; the game adds its network services (`IService`), commands and logic and
registers an `ILoggerFactory`. `SessionCreateOptions`: type, expected users (bots included), places (`MaxUsers`,
bots take none), `JoinTimeout`, `ReconnectWindow` (zero - a lost connection is a leave; otherwise the seat waits
and the others get `RemoteAway`). A host client has the same: `LocalSessionServer.Create(..., reconnectWindow)`
(`SessionCore` with `isReconnectable`, `SeatAway`, `ExpireAway`; the window is counted by the host in real time). `IUser.Id` is the id of the user in this session (sent to the clients),
`IUser.BackendId` the meta id (never sent). Bots: `IUserFactory.CreateBot`. Entities built by the backend:
`IEntityFactory.Create(seat)`. Users and seats are created in the execution queue of the session only. The factory
of the sessions, the gateway and the search stay in the game. `ISession.Reserve(userId)` returns `SessionReservation`, the
reason of a refusal (`HeldOnlyByOwnStale`: only a stale connection of the same user holds it, the session is closing). The
owner of a `Connection` subscribes its `Reader` before `Run` (`Run` without a handler throws) and returns only after
`Closed`: a socket that ends with the handler takes the close frame with it. The server sends no requests to a client; a
message it cannot take closes the connection; a handshake is limited to 16 KiB.
