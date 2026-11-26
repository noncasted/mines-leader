# Match history: the read API

Read only, for agents (the `match-history` skill) and people with the console token. Lives in MetaGateway next to
`/api/client-telemetry` and `/api/match-metrics`: `app.MapMatchHistoryRead().RequireConsoleToken()` in `Program.cs`. Code:
`MatchHistoryEndpoints`, `MatchHistoryPlayerEndpoints` and the answers next to this file; the players of a match are built in
`backend/Meta/Matches/Players` (`MatchPlayerCards`, `MatchPlayerSeriesReader`, `MatchPlayerSeriesCsv`), the console reads
them the same way.

## Access

- Only `GET`. The token in the header `Authorization: Bearer <CONSOLE_TOKEN>` (the same token as the console, the client
  telemetry and the match metrics). Without it or with a wrong one: 401. In Development with the development token the
  check is off (`ConsoleAccess`).
- https on the public domain of the meta, http only locally.

## Where the data comes from

The record of a match is the grain `Meta.Matches.Match` (`IMatch`, `MatchState`), the key is the id of the game session of
the match (the `SessionId` of its clients). Written by the game server only (`Game.GamePlay.GameFlow`): the setup at the
start of a round (the type, the participants with the deck and the name of each), the end with a winner (`OnComplete`: the
winner, the rating changes) or without one (`Close`: the round ended without a winner, or the session ended during it).
A rematch is a new session (`MatchHandle` creates it): a new id and a new record. Bots are participants too.

Every record is kept in memory of the host in `IMatchSummaryCollection`: the lists and the card of a match are read from
there, without the database. A change of a record reaches the host a moment after the grain (a durable queue).

The players of a match (their totals, events and series) are the streams of the collector of the game metrics
(`tools/framework/backend/MatchMetrics`, `IMatchMetricsReader` in the process; its raw reading is `/api/match-metrics/...`).
The client of a player writes its own stream (a new one after a reconnect). The names of the series and events:
`Meta.Matches.MatchGameMetrics` (the contract of the game metrics, `docs/tasks/current/telemetry_port/telemetry_port_info.md`).
A player whose client wrote nothing (a bot, an old client) has `streams: 0`, zero totals and no events.

## Answers

camelCase JSON, enums as their C# names (`LastManStandingTurnBased`, `Trinity`). Times are UTC ISO 8601 of the server. A
field without a value is an explicit `null`.

A **row** (`MatchHistoryRow`) - the same for the lists and the card of one match:

```
{ id, type, startedAt, closedAt, durationSeconds, winner,
  players: [{ userId, nickname, deck: [<CardType>], isWinner, ratingChange }] }
```

- `type`: `Shared.GameMatchType` (`Single`, `TimeLimited`, `LastManStanding`, `LastManStandingTurnBased`).
- `closedAt`, `durationSeconds`: `null` while the match runs.
- `winner`: the meta id of the winner; `null` while the match runs and for a match closed without a winner.
- `players`: the participants in the order of the setup; `nickname` is the name at the setup; `ratingChange` is 0 without
  a winner.

## Endpoints

### `GET /api/match-history/matches`

Rows, newest by `startedAt` first. Nothing found is `[]`.

| Parameter | Meaning |
|-----------|---------|
| `player` | a meta id (a guid): the matches of this participant; any other text: a participant whose `nickname` is this one, case ignored |
| `type` | a `GameMatchType` name |
| `winner` | `true` - only matches with a winner, `false` - without one (running ones included) |
| `from`, `to` | the start of a match, ISO 8601 with an offset (`2026-11-17T12:00:00Z`): `from` inclusive, `to` exclusive |
| `closed` | `true` - only closed matches, `false` - only running ones |
| `offset`, `limit` | the page: `offset` from 0, `limit` from 1 to 1000, 50 by default |

All the given conditions together.

### `GET /api/match-history/matches/{id}`

The row of one match; 404 when this host knows no such match.

### `GET /api/match-history/players/{player}?limit=`

The matches of one user, newest first:

```
{ userId, nickname, total, matches: [<row>] }
```

- `player`: a meta id, or the name of the user now (case ignored). 404: no such user. 409: several users have this name,
  the text lists their ids - ask by the id.
- `nickname` is the name now; the rows keep the name at the setup.
- `total`: all the matches of the user, `limit` (1 to 1000, 50 by default) cuts `matches`.

### `GET /api/match-history/matches/{id}/players`

A card per participant, in the order of the setup; 404 when this host knows no such match.

```
[{ userId, nickname, isWinner, ratingChange, streams,
   cellsOpened, cellsBySource: { click|cascade|card: n },
   counters: { cascades, flags.set, flags.removed, mines.hit, turns },
   cardsPlayed, cardsByType: { <card>: n },
   events: [{ name, time, at, fields }] }]
```

- `streams`: the client streams of the participant in the match, 0 - no metrics.
- `cellsBySource`, `cardsByType`: by the tag `source` / `card` (`card` - `CardType` in lower case); `""` - a series without it.
- `events`: `turn.end {number, seconds, cells, cards}`, `card.play {card, board, cells}`, `match.end {result, seconds}`, by
  `time` - seconds of the match clock; `at` - the server time of its batch (UTC). A field the client did not know is absent.

### `GET /api/match-history/matches/{id}/players/{userId}/series?step=10&axis=run`

The counters of one participant by steps; 404: no such match or the user is not its participant.

| Parameter | Meaning |
|-----------|---------|
| `step` | `1`, `10` (default) or `60` seconds |
| `axis` | `run` (default) - seconds of the match clock; `server` - unix seconds of the server time |

```
{ userId, step, axis, lines: [{ key, points: [{ time, value }] }] }
```

- `key`: the name of the series, `<name>:<tag value>` for a tagged one: `cells.opened:click|cascade|card`, `cascades`,
  `flags.set`, `flags.removed`, `mines.hit`, `turns`, `cards.played:<card>`. A counter without points has no line.
- `time` of a point - the start of its step on the axis; a value is the sum of the step; a step without values has no point.

### `GET /api/match-history/matches/{id}/players/{userId}/series.csv?step=10&axis=run`

The same series as `text/csv`, `\n`, numbers of the invariant culture, no quotes: the header `time,series,value` and a row
per point, by the line and the time. A player without metrics: the header only.

## Errors

400: a parameter out of its form or range (also a value the binder cannot read), or a query the collector refuses. 401: no
token. 404, 409: above. 503: the database of the collector could not answer; the reader may repeat.
