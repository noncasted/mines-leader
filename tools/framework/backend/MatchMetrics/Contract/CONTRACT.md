# Match metrics: the contract of the writing

MemoryPack messages over the meta socket of a client (`shared/MatchMetrics/SharedMatchMetrics`), union tags
`NetworkContextTags.MatchMetrics*` 220-223. The reading (API) - the section Reading below.

## Messages

| Request | Response | Meaning |
|---------|----------|---------|
| `Open { MatchId, WriterId, Kind }` | `Opened { Status, StreamId, LastSeq }` | opens the stream of (match, user of the socket, writer) or finds it again: same `StreamId`, `LastSeq` - the last stored batch (0 none) |
| `Batch { StreamId, Seq, Series, Chunks, Events }` | `BatchResult { Status }` | one batch in flight per stream; `Seq` from 1, +1 per batch |

- The user is the one of the socket (`IUserSession.UserId`, the meta id), never sent by the client. A client opens only
  `Kind = Client`; `Server` is the stream of the server (`IMatchMetricsWriter`, user `Guid.Empty`).
- A batch reaches only a stream opened on the same socket; after a reconnect the client opens again with the same `WriterId`.
- `Opened.Status`: `Ok`; `NotInMatch` - the admission of the game says the user has no seat in the match (or the match is
  unknown); `Closed` - the match closed more than `CloseGrace` (2 min) ago; `Rejected` - a client asked for a server stream.
- `BatchResult.Status`: `Ok` stored; `Duplicate` - this `Seq` is stored already (a copy after a lost answer), nothing written,
  the client goes on; `Rejected` - a gap in `Seq` or a broken rule below, the client stops the stream; `Closed` - as above,
  the client stops.
- A failure of the server (database, admission) is an error response: the client keeps the batch and sends it again.
- The admission is asked on `Open` and again by a batch when its answer is older than `AdmissionRecheck` (60 s).

## Rules of a batch (`MatchMetricsBatchParser`; `SharedMatchMetricNames`, `SharedMatchMetricsLimits`, `MatchMetricsLimits`)

- Names of series and events `[a-z0-9._]` up to 64; tag and field keys `[a-z0-9._]` up to 32; tag values and field texts
  `[a-z0-9._-]` up to 32. No free text.
- `Series` - the series first used in this batch: a new slot 0..255 of the stream, not declared before; up to 4 tags, no key
  twice; `Kind` `Counter` or `Gauge`. The same name and tags (in any order) are one series of the game; another kind for them
  is rejected. A series is in one slot of a stream: declared twice in a batch or again in another slot - rejected.
- `Chunks`: a declared slot, at least one value, buckets `Start .. Start + n` within 0..86400, up to 4096 chunks and 65536
  values a batch; the chunks of one slot in a batch do not overlap (no bucket twice, no two with one `Start`); between batches
  the server does not check it: a bucket sent again in a later batch is stored again, a counter sums both, a gauge shows either
  of them as its point (the chunks are read without an order) and counts both in its totals (a client sends a bucket once); no infinity; `NaN` only in a gauge (unchanged in the bucket); `AgeMs` 0..86 400 000.
- `Events`: up to 1024, `Time` finite 0..86400, up to 8 fields with distinct keys, a number finite or a text.

## Storage (`MatchMetricsSchema`)

Tables created by the deploy setup of a game: `match_metric_series` (dictionary), `match_metric_streams`,
`match_metric_slots` (the slot map of a stream, for a reconnect to another gateway), `match_metric_chunks` (`values real[]`
from bucket `start`, `at` = receive time - `AgeMs`), `match_metric_totals` (sum, count, max per stream and series, NaN not
counted), `match_metric_events` (`fields jsonb`). A batch is one transaction: `last_seq` moves from `Seq - 1` only.

## Reading (`IMatchMetricsReader`, `MapMatchMetricsRead`)

In the process: `AddMatchMetricsReader()`, `IMatchMetricsReader`. Over HTTP: GET only, under the console token of the game
(`MapMatchMetricsRead().RequireConsoleToken()`), JSON in camelCase, enums as words (`client`, `server`, `counter`, `gauge`).

| Route | Query | Answer |
|-------|-------|--------|
| `/api/match-metrics/series` | `name` | the dictionary: `[{ id, name, kind, tags }]` |
| `/api/match-metrics/totals` | filter, `since`, `until` (ISO, on the open time of a stream) | totals across matches |
| `/api/match-metrics/matches/{matchId}/streams` | | `[{ id, matchId, userId, writerId, kind, openedAt, lastSeq, lastAt }]` |
| `/api/match-metrics/matches/{matchId}/series` | filter, `step` 1/10/60, `axis` run/server, `from`, `to` | `[{ name, group, kind, points: [{ time, value }] }]` |
| `/api/match-metrics/matches/{matchId}/totals` | filter | `[{ name, group, kind, sum, count, max, streams }]` |
| `/api/match-metrics/matches/{matchId}/events` | `name`, `user`, `from`, `to` (seconds of the run) | `[{ streamId, userId, kind, seq, index, time, at, name, fields }]`, by time |

- The filter: `name`; `user` (the meta id, `00000000-...` - the server); `tag.<key>=<value>` - the series having this tag (any
  count); `groupBy=<key>,<key>` - tag keys and the pseudo key `user`. A group is always one name; a series without a tag of the
  group has `""` for it. Without `groupBy` all the series of a name are one group.
- Points: a counter is the sum of its buckets in a step; a gauge is the last value of a series in a step (`NaN` - unchanged),
  carried to the next steps of its group, the series of a group summed. `time` is the start of a step: seconds of the run
  (`axis=run`) or unix seconds of the server time (`axis=server`, from the `at` of a chunk); `from` / `to` - inclusive, in the
  same unit. The points of a match are summed in memory of the host.
- Totals are summed in SQL over `match_metric_totals`: `sum`, `count` of the values, `max`, `streams` - the streams in the group.
- A name, a tag or a key out of the rule of names, a key twice, a step or an axis not listed, a group of counters and gauges -
  400 with the reason; a failure of the database (`DbException`) or a timeout - 503 (repeat); any other failure - 500. Every value of a query is a parameter of SQL.
