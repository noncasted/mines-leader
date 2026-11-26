# Client telemetry: the contract

MemoryPack messages over the meta socket of a client (`shared/ClientTelemetry/SharedClientTelemetry`), or the same messages as JSON
over an HTTP link (below, "HTTP"); never the socket of a game session. Union tags `NetworkContextTags.Telemetry*`, 200-206: a project
with the socket lists them in its `NetworkContextUnion`. "Socket" below means either: a meta socket or an HTTP link.
Limits: `ClientTelemetryLimits`. A session is a part of the game the game names with its tags (a menu, a match): the framework
knows no values of them. Nothing is sent outside a session but the startup trace (below).

## Messages

All are requests with a response, one socket holds at most one open session.

| Request | Response | Meaning |
|---------|----------|---------|
| `SessionStart { Device, Tags, StartupId, Previous }` | `SessionStarted { Enabled, SessionId }` | closes the session still open on this socket (status `ended`), opens a new one; the guid comes from the server, random; the client keeps it only in its own storage while the session is open (see `Previous`) |
| `Batch { SessionId, Names, Samples, Errors, Marks }` | `BatchResult { Enabled }` | one in flight: the next one goes after the answer |
| `SessionEnd { SessionId }` | `EmptyResponse` | closes the session with status `ended`; the last batch goes before it |
| `StartupTrace { StartupId, Device, Name, Final, DurationMs, Frames, Spans }` | `StartupTraceResult { Enabled, StartupId }` | stores a snapshot or the final trace of the start of a client; needs no session |

Null lists are empty. `Time` is milliseconds since the start of the session, no clock of the device.

- `Names`: `{ Name, Unit }`, the new names of the session; each takes the next slot.
- `Samples`: `{ Time, Values, Hidden }`; `Values[i]` belongs to slot `i`, `NaN` is no value in this sample, a shorter array leaves the
  rest empty; `Hidden`: the page was in the background, the sample stays out of the summary.
- `Errors`: `{ Time, Level, Message, Stack, Repeat, Category }`; `Level` is `Error`, `Exception` or `Warning` (the warnings of the log of Unity,
  under their own client limits), `Repeat` from 1. The digest counts the warnings apart (`warningCount`): `errorRows`, `errorCount` and
  `minErrors` hold none of them; the groups hold both, by level. `Category` (last member of the message): the category of the logger of
  the framework that wrote the error, the full name of a type (`Global.Network.PeerSocket`, generic arguments by their short names:
  `Global.Network.Pool<Packet>`); `Message` is then its template with the `{Name}` placeholders, not the formatted text. An exception
  logged through it has the message `<template>\n<ExceptionFullType>: <text>` (or `<ExceptionFullType>: <text>` without a template),
  the level `Exception` and its own stack. Null: an error of the log of Unity, of the browser or of a task nobody awaited.
- `Marks`: `{ Time, Name, Detail, DurationMs }`; `Detail` a text of the code next to the name (the key of a loaded asset), scrubbed,
  null for none; `DurationMs` what the mark closes, null for none. The client marks the loads of assets this way: `load.start`,
  `load.end`, `load.failed` (the type of the error in the detail), `load.slow`, `load.unfinished` (at the end of the session).
  The logger of the framework marks its records at or above the mark level of their category (`LogOptions` of the `OptionsContainer`
  of the game) as `log.debug`, `log.info`, `log.warning`, `log.error`, the detail `<ShortTypeName>: <template>`.
- `Device`, the closed list: `Platform`, `BuildVersion`, `Browser` + `BrowserMajor`, `Os` + `OsMajor` (names without digits, the
  major version apart), `Gpu`, `Cores`, `MemoryClass` (GB: a power of two from 0.25 to 1024), `ScreenWidth`, `ScreenHeight`, `Dpr`. 0
  or null is unknown.
- `Tags`: named in the code of the game, `[a-z0-9._]` up to 64 characters, up to 8. The others are dropped and counted in
  `rejectedRequests`; the start goes on.

**`Enabled` false** stops the sending until the next `SessionStart`: nothing is retried. It answers:
- the flag is off: a start creates nothing, a batch is not written and closes the open session (status `ended`);
- a batch of a session that is not the one open on this socket: another socket, an older session, a closed one; it is dropped. So a
  client ignores the answers to the batches of a session it has left;
- a second batch while one is in flight;
- a failure of the cluster (logged without the contents), or a session the grain has already closed.

A batch of 30 metrics with 2 samples (every 2 seconds) is under 600 bytes, the first one with the names under 1500.

**The socket decides.** Its close (for an HTTP link: `IdleAfter` without a request) counts as a crash of the client: no retry, no
reconnect, no queue. The end of the lifetime of the
connection closes its session with status `lost`, with the summary and the log. A failure of that write is logged there and never
reaches the gateway.

## The previous session

`SessionStart.Previous { SessionId, Tags, LastSampleTime, SinceMs, Marks, SampleNames, Samples }`: the session of the same browser (its local storage) before
this one that did not close on the client: the page was killed, reloaded, closed or frozen inside it. Null when the previous one
closed. The client writes its record at every sample and mark of an open session and removes it at the close; the next start takes
it once. `LastSampleTime` and the `Time` of the marks are of that session, `SinceMs` is the time from its last record to this start
(-1 unknown); the marks are the last ones it took (up to `MaxPreviousMarks`), also those the server never got, with the `Detail` and
`DurationMs` of a batch (a page that died inside a load names the load). No clock of the device and
nothing personal: the id is the random guid the server gave. A tab open next to another one may report a session that is still alive.

`Samples` are the last samples it took (`{ Time, Values, Hidden }` of a batch, oldest first), with the main numbers of a frame only:
`Values` go by the slots of `SampleNames` (not the names of the session), NaN no value. The client sends up to 5 of them by about 7
names (`frame.dt.max`, `frame.cpu.main`, `frame.phase.update`, `frame.phase.postlate`, `web.raf.dt.max`, `render.draw_calls`,
`web.gl.shader_compile_ms`); null lists are none (a client before them).

The server keeps it with the new session (`previous` of a row and a card, explicit `null` when there is none) and finds the next
session of a lost one: `sessions?previousId=`. An empty id drops the whole; a tag, a mark (a negative duration too) or a time out of
form (or over `MaxPreviousMs`) is dropped alone, counted in `rejectedRequests`; a detail is scrubbed and cut as in a batch.

The samples are checked the same way, each part counted once in `rejectedRequests`: a name out of the form of a metric name (`[a-z0-9._]`,
up to 64), null or repeated is dropped with the values of its slot; the names over `MaxPreviousSampleNames` (16) are cut with their values;
a null sample, a negative `Time` or one over `MaxPreviousMs` drops the sample; over `MaxPreviousSamples` (8) the oldest ones are cut; an
infinite value is no value and counts; values past the end of `SampleNames` are ignored and count once for the sample; NaN is no value
and does not count. The answers hold `previous.samples: [{ time, hidden, values: { "<name>": <value> } }]`, a metric without a value
absent from `values` (as in `/samples`); `[]` when there are none, also in a row written before the samples.

## The startup trace

The only exception from "nothing outside a session", under the same flag. `Guid.Empty` as `StartupId` asks for a new record, the id comes
from the server and lives as long as the socket (after a reconnect a client starts a new record).

- `Spans`: `{ ParentIndex, Name, StartMs, DurationMs, Frames, Unfinished, AllocBytes, File, TransferBytes, Cached }`; `ParentIndex` is
  below the index of the span, -1 for a root; times from the start of the trace; `AllocBytes` 0 where not measured. `File`: a file the
  page loaded before the build, by the last segment of its address only, null for any other span; `TransferBytes` what came over the
  network (0 unknown), `Cached` from the cache of the browser.
- On the web the trace starts at the origin of the page (`performance.timeOrigin`): the first root `Page` holds the navigation, the
  parsing, the files, the gap to the start of the build (`Build start`) and the spans of the template; the spans of the build follow
  on the same time line.
- The final trace replaces the snapshot of the same record; a snapshot after the final one changes nothing (`Enabled` true). A closed
  socket changes nothing: a snapshot stays with `final` false. The time of a record is the time of the server, no clock of the device.
- `Enabled` false: nothing is written and the client sends no more traces on this socket. The flag is off, the id was not given on this
  socket, a failure, or the limits of a socket (`MaxStartupsPerSocket`, `MaxStartupWritesPerSocket`). It says nothing about the sessions.
- `SessionStart.StartupId`: the record of this client, `Guid.Empty` for none. An id not given on this socket is not kept and counts
  in `rejectedRequests`; the start goes on. Sessions refer to a record and to the previous session that did not close (above), and no user id is involved.
- `BackendTraceId`: not sent by the client. The game passes the trace of the authorization of the socket in the OTLP backend to
  `AcceptStartup(trace, backendTrace)`, the record keeps it as 32 hex digits (`default`: null) and the last write of the record sets it
  (every write of a record comes from one socket). It ties the start of a client to the spans of the backend (Aspire dashboard: Traces,
  search by the id). A random id of a trace, not personal data: it names no user and no address.
- **The server checks:** the spans (4096) and the estimated size (256 KB: a typical start is about 30 KB) cut the tail, counted in
  `droppedSpans`; a name is cut to 128 characters (`cutNames`) and goes through `Scrubber`, so a URL in a span of a request reads `GET <url>`;
  a null or empty name, a control symbol, a time not finite, negative or over `MaxStartupMs`, negative frames or bytes, a `ParentIndex`
  not below the index: the span goes with its children, counted in `rejectedSpans`. A `File` out of the form of a name (words of
  `[A-Za-z0-9_-]` between dots, up to 64) is dropped, the span stays; negative `TransferBytes` drop the span.

## HTTP

For a client that cannot open the meta socket (a page in an iframe that reaches only its own server). The client:
`AddHttpClientTelemetry(options, TelemetryHttpOptions)`; the server: `MapClientTelemetryHttpIntake()` on a group of the game.

- **The routes**, `POST` with `Content-Type: application/json`, relative to the group: `/start`, `/batch`, `/end`, `/startup` - the
  four requests above. The answers are those of the socket in camelCase: `{ enabled, sessionId }`, `{ enabled }`, `{}`,
  `{ enabled, startupId }`. 400: a body that is not this JSON (`NaN` in it too) or without `linkId`. The intake knows no game and no
  authorization: the game maps it on its group and puts its filter there (`app.MapGroup(...).AddEndpointFilter(...).MapClientTelemetryHttpIntake()`).
- **The link.** Every request holds `linkId`, a random guid the client makes per page (per transport); the server keeps a link by it
  as a socket: a session, the startup ids given on it. A link without a request for `ClientTelemetryHttpOptions.IdleAfter` (60 s, must be
  below `LostAfter`) is a closed socket: its session closes as `lost`. A request with an unknown id opens a new link (the batch of an
  older session gets `enabled` false). The client takes a new link after any request that failed on the way (a network failure, the
  timeout of `TelemetryHttpOptions`, a status that is not 2xx): the server may have taken it or not, so the old link counts as lost.
  Nothing is repeated. A page in the background that stops sending loses its session after `IdleAfter`, where a socket keeps it.
- **The JSON** is written by `JsonUtility`, which writes no null: the names are those of the messages in camelCase; guids are their
  text, `Guid.Empty` for none (also `previous.sessionId` when there is no previous); an empty text is none (`unit`, `detail`, `stack`,
  the `category` of an error, `file`, the texts of `device`); 0 a number not known (`device`); `level` by name (`Error`, `Exception`, `Warning`; another one drops
  the error); `hasPrevious` false: no `previous` (its object is there, ignored); a mark with `hasDuration` false closes nothing
  (`durationMs` ignored).
- **NaN.** JSON has no `NaN` and no infinity. A sample is `{ time, values, empty, hidden }`: `empty` lists the slots of `values` without
  a value (their numbers, written as 0, are ignored; an index out of `values` is ignored); the client writes an infinite value as empty
  too. The same in `previous.samples`. A number of the trace or of the device is finite.

## What the game does

`AddClientTelemetry()` on the gateway registers `IClientTelemetryConnections` and the links of the HTTP intake. The four commands of
the meta socket stay in the game: `telemetry.Of(session.Lifetime).Start(request)`, `.Accept(request)`, `.End(request)`,
`.AcceptStartup(request, backendTrace)` (the trace of the authorization of the socket, `default` when the game has none). Over HTTP: `MapClientTelemetryHttpIntake()` on the group of the game, nothing else.

## Reading

`MapClientTelemetryRead()`, under the token the game puts on the group. Answers in camelCase, statuses and levels as words.

A live session is read from the memory of its grain, without the database. A closed one: the row and the log from the states,
without a grain.

- `GET /api/client-telemetry/sessions?status=live|ended|lost&platform=&build=&tag=&metric=&averageBelow=&averageAbove=&minErrors=&startupId=&previousId=&offset=&limit=`,
  newest first, from the collection of the sessions in memory of the host. `tag` is an exact match, several of them mean all of
  them. `build` is an exact match of `BuildVersion`; `startupId` gives the sessions of one startup record, `previousId` the session
  that reported the given one as its previous. Each row and the card of a session hold `tags` (`[]` when there are none, also in a row written before the tags). `metric` with no average: sessions that have the metric in the summary;
  `averageBelow`, `averageAbove` need `metric` (400 otherwise). A live session has no summary yet, so `metric` and `minErrors` leave it out.
- `GET .../sessions/{id}`: `ackSeq` is the `seq` of the last batch in memory, `after` of a live reading (the name stays for the skill); `digest` (the summary of the close, the counts of errors and marks, the dropped counters, `errorGroups:
  { groups: [{ fingerprint, level, message, count, rows, firstSeenAt, lastSeenAt, category }], omittedGroups, omittedCount }` with up to 12
  groups) is null
  while it is live and for a session closed without its data.
- `GET .../sessions/{id}/summary?from=&to=`: the window in `t` of the session, inclusive. `{ samples, hiddenSamples, metrics: [{ name,
  unit, count, average, p50, p95, min, max, growth }] }`. Hidden samples are out of everything; percentiles are by the nearest
  rank; `growth` (last minus first value) only for the unit `bytes`, else null; a metric without a point is not listed.
- `GET .../sessions/{id}/samples?after=<seq>&wait=<0-30 s>&limit=`: `{ status, lastSeq, items: [{ seq, time, hidden, receivedAt,
  values: { <name>: <value> } }] }`. A metric without a value is absent from `values`. `lastSeq` goes into the next `after`.
  With `wait` the request asks the grain every 500 ms until rows come, never the database; it ends when the session closes.
  An empty `items` at the end of the wait is the normal answer. Works with several gateways.
- `GET .../sessions/{id}/marks?after=&limit=`, `.../errors?after=&limit=`: the same page without `wait`. A mark holds `detail` and
  `durationMs`, null when absent (also in a row written before them).
  A row of `errors` has its `category` (null for none, also in a row written before it) and its `fingerprint`: 16 hex digits of a
  SHA-256 over the level, the message and the stack, already scrubbed and without the `<n>` and `<id>` left in place of numbers and
  addresses, and the category when there is one (one template in two categories is two kinds; without a category the hash is the one
  of before it, so the stored groups still match). The same for another line or address, also with an empty stack (then only the
  level, the message and the category decide).
- `GET /api/client-telemetry/errors?limit=`: the kinds of errors over the digests in memory of the host, by `count` (with
  repeats), of equal counts by `lastSeenAt`. `{ totalGroups, countedSessions, sessionsWithoutDigest, omittedGroups, omittedCount,
  droppedErrors, groups: [{ fingerprint, level, message, count, sessions, firstSeenAt, lastSeenAt, sessionIds, category }] }`. `message` is
  cut to 120 characters, `sessionIds` holds up to 20 of the freshest sessions, `sessions` counts them all. A session counts from its
  close: a live one and one lost in a crash have no digest (`sessionsWithoutDigest`).

- `GET /api/client-telemetry/startups?limit=`: the records, newest first: `{ id, device, name, final, receivedAt, updatedAt, durationMs, frames,
  spanCount, depth, unfinishedSpans, droppedSpans, rejectedSpans, cutNames, backendTraceId }`; `backendTraceId` null when the game passed none
  (always over HTTP).
- `GET .../startups/{id}`: `{ startup, spans: [{ index, parentIndex, depth, name, startMs, durationMs, ownMs, frames, unfinished, allocBytes,
  file, transferBytes, cached }],
  sessions }`. `ownMs` is the duration without the time covered by the children, overlaps counted once. `sessions` are rows of the list.
- A row and the card of a session hold `startupId` (explicit `null` when there is none); `sessions?startupId=` filters by it.

404: no such session or record. 400: a query out of range.

## The flag

`IClientTelemetrySwitch`, off by default, set on the Features page of the console (with a record in the audit log). It is not one
of `ClusterFeatures.GameFlags`: those are keyed by the deploy id and the coordinator generates a new id on every process start.

The value is one row of `ClientTelemetrySettingsState` in `configs`, with a fixed key and no deploy id: it survives a new deploy and a restart
of the coordinator. A host reads the row once when it starts and then holds the value in memory: a message makes no query for it. A change
goes to the other hosts by the channel of `AddressableState`; a gap of the channel reloads the row. Until the first read ends
the flag reads as off: a session started in that moment is not tracked, and that is all.

## What the server does with a message

The client is not trusted; `TelemetryBatchParser` is the only way into the model. Telemetry never breaks the socket of a player.

- **Rejects the rows of a batch**, counted in `rejectedRequests`: over `MaxBatchBytes` (estimated), too many items of a list, too long
  `Message` or `Stack`, a negative time, a repeat under 1, an infinite value, a null item, more than 200 names per session or values
  without a name (`TelemetrySessionLimits`). The names of the batch stay when they fit: the client counts its values by the slots.
- **Drops one item, the batch goes on:** a name or a mark out of `[a-z0-9._]` or over 64 characters (a dropped name keeps its slot), a
  unit by the same rule (the name stays), the `Category` of an error that is not a name of a type (words of `[A-Za-z0-9_]` from a
  letter or `_` between `.`, `<`, `>`, `, ` between generic arguments) or over 256 characters (`MaxCategoryLength`; the error stays
  without it), an error of another level, a field of `Device` of a wrong form, the `Detail` of a mark with a
  control symbol or blank (the mark stays; a long one is cut to 128 and goes through `Scrubber`). A negative `DurationMs` rejects the
  rows like a negative time.
- **Scrubs** `Message` and `Stack` (`Scrubber`); the client scrubs them too, by the same corpus (`Tests/ClientTelemetry/Golden/pii_corpus.json`).
  A braced text becomes `{<str>}`, except a placeholder of a template of the logger: `{Identifier}`, a letter or `_` and then letters,
  digits or `_`, 32 characters at most. It exposes nothing more than a plain word, which passes anyway (and a word of 32 characters and
  more, or of hex digits, is still `<id>` inside the braces). The category is a name of the code and is not scrubbed.

The limit of a plain word: `Scrubber` cannot tell a name written without quotes or a key ("Player Ivan left") from the
template. Such a text must not get into the messages of the code.

## What the storage does

One grain per session (`ClientTelemetrySession`, the guid is its key). The samples, errors and marks stay in its memory. Two states:
`TelemetrySessionState` (metadata with the tags and the compact summary, for the list and the search) and `TelemetrySessionLogState`
(everything). No user id, no address and no user agent in either: the gateway does not pass them. The startup record keeps the id of
the backend trace of its socket (`BackendTraceId`): a random id, not personal data.

- **Two writes.** The metadata before the start is answered; the summary and the log at the close, in one batch.
- **`seq`** of a row is the number of its batch in the session, the cursor of the reading.
- **A slot is never lost.** A dropped name stays an empty slot. A name the session already has takes a slot too, but stays empty: the
  first slot is the only series of a name.
- **The memory is bounded** (`ClientTelemetryStorageOptions`: `MaxSamples`, `MaxErrors`, `MaxMarks`): over the limit the oldest rows
  go and are counted (`droppedSamples`, `droppedErrors`, `droppedMarks`).

The close, once; a closed session never opens again:
- **`SessionEnd`, the next `SessionStart`, a batch under the flag off:** status `ended`. A failed write is repeated by the next tick
  of the timer with the status asked for, so a clean close never reads as `lost`.
- **The end of the socket** (of an HTTP link: `IdleAfter` without a request): status `lost`.
- **No batch for `LostAfter`** (120 s, the timer of the grain): status `lost`. The reserve for a gateway gone without closing its
  sockets, so a page in the background, which sends rarely, keeps its session. A failed write is repeated by the next tick.
- **A deactivation of a live grain** (a stop of the silo, a migration): status `lost`, the same write.

**After a crash** of a silo the memory is lost. The next activation of the session (a batch of its client, or the sweep) closes the
row as `lost` without a log and a digest; the client gets `Enabled` false.

**A startup record** (`ClientTelemetryStartup`, a grain without memory): `TelemetryStartupState` (metadata, in the collection of the hosts)
and `TelemetryStartupTraceState` (the spans), written together on every message and read without a grain. It goes `RetentionDays` after its last write, by the sweep.

**The sweep** (`ClientTelemetrySweep`, every hour on each silo): sessions the collection knows as open go to their grains, a live grain
ignores it and the others close as above; both states of a session closed more than `RetentionDays` (7) ago are deleted. A grain
the sweep reaches tells the hosts about its row also when nothing changes (closed and not expired, no row, a fresh startup record): a
host that missed a notification (a failed push after the write or the delete) learns the row by its next pass.
