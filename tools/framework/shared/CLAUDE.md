# Shared

Protocol and types used by both the Unity client and the backend: `Protocol/` (messages, union tags), `Session/` (the
real-time session), `Lobbies/` and `Matchmaking/` (lobby rooms and the session requests of the meta), `ClientTelemetry/` (the
telemetry of a client over its meta socket). The contract of a part is written with its backend: `backend/Session/CLAUDE.md`,
`backend/Hosting/CLAUDE.md` (lobbies, matchmaking), `backend/ClientTelemetry/CLAUDE.md`.

## Protocol

`INetworkContext` is a plain interface; the project declares the union
(`[MemoryPackUnionFormatter(typeof(INetworkContext))] partial class NetworkContextUnion`) with the framework tags
(`NetworkContextTags`, 0-249) and its own (1000+), and registers it on start (backend: `NetworkProtocol.Register()`
in the service defaults, Unity: on load). Members of type `INetworkContext` are `[MemoryPackAllowSerialize]`.
The protocol hash belongs to the project (`ClientProtocolCheck.IsCurrent(context, hash)`). A project with real-time
sessions lists the session tags too (`NetworkContextTags.Session*`, `Entity*`, `Object*`, `Player*`, `Service*`,
`Latency*`, `Peer*`) and declares the unions over the payload interfaces of `shared/Session` the same way
(`[MemoryPackUnionFormatter(typeof(IEntityPayload))] partial class EntityPayloadUnion`, also `IEventPayload`,
`IStatePayload` when used), registered together with `NetworkContextUnion`.
