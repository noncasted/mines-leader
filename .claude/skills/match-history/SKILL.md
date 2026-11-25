---
name: match-history
description: Read the history of the real matches from the meta backend over HTTP and analyse them - the last matches by player, type, winner, closed and date, one match whole (participants, decks, rating changes, cells by source, flags, mines, cascades, turns with their time, card plays), the matches of a player, the series of a participant by steps (a table or csv), and an analysis of the players of a match against each other and of the cards played and in decks against the outcome over several matches. Use when the user runs /match-history, asks about the match history, the last matches, to analyse a match, which cards win in real matches, how long the turns are, or to export the series of a match.
---

# AUTO-EXECUTE - DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /match-history, match-history, история матчей, последние матчи, анализ матча, разбери матч, матчи игрока, какие карты сильные по реальным матчам, сколько длится ход, выгрузка серий матча, match history, last matches, analyse a match, strong cards in real matches, turn time
BEHAVIOR: When triggered, run the command that fits the request and report from its output.

# Match History

The API is `backend/Orchestration/MetaGateway/Matches/CONTRACT.md` (the row of a match from the record of the meta, the
series and the events of its players from the collector of the game metrics; the names - `backend/Meta/Matches/Players/MatchGameMetrics.cs`).
The script only reads (GET).

```
H="python3 .claude/skills/match-history/history.py"    # from the root of the project
```

## Rules

- Read only. The token (`CONSOLE_TOKEN`) is read from the env file of the section `## client-telemetry` of
  `.claude/project-skills.md` (the same meta, the same token): never print it, never pass it as an argument, never `cat`
  the env file. The script cuts it from its output; redirects are refused, plain http only to localhost.
- Another meta: `--url <u> --env-file <f>` after the subcommand (the local cluster: `--url http://localhost:7301`, see the
  section `client-telemetry` of `.claude/project-skills.md`).
- The cards come as the backend has them: a deck by the C# name of `CardType` (`Trebuchet_Max`), the metrics by the tag
  `card` - the same name in lower case (`trebuchet_max`).
- Bots are participants: they have a deck and may win, but no metrics (`streams=0`), like a client from before the metrics.
- Exit codes: 0 ok, 1 the service (unreachable, 5xx, 400, 409, a redirect), 2 usage / config, 3 token refused, 4 not found.
  503 - the collector could not answer: repeat later.

## Commands

```
$H matches [--player P] [--type T] [--winner yes|no] [--closed yes|no] [--from T] [--to T] [--limit N]
$H match <id>                          # the row, the participants with decks; per player: cells, counters, cards, turns, card plays
$H player <nickname|id> [--limit N]    # the matches of a user (a nickname of several users: 409 lists the ids)
$H series <id> <userId> [--step 1|10|60] [--axis run|server]               # the counters by steps, a table with totals
$H export <id> <userId> --out docs/tasks/current/<task>/x.csv [--step] [--axis]   # the same as csv: time,series,value
$H analyze <id>                        # one match: the players side by side
$H analyze --last N [filters]          # the last N closed matches and a summary over them
```

`--type` is a `GameMatchType` (`Single`, `TimeLimited`, `LastManStanding`, `LastManStandingTurnBased`). `--from` / `--to`
are ISO 8601 with an offset (`2026-11-17T12:00:00Z`), the start of a match. `--axis run` is the match clock (seconds from
the start of the game, one for both players), `server` the unix seconds of the server.

## Reading `analyze`

- Per player: the result of the client (`match.end`: `win` / `lose` / `leave`) next to the winner of the record, turns,
  the mean length of a turn (`turn.end.seconds`), cells and cells per turn, the shares of cells by `click` / `cascade` /
  `card`, mines hit, flags set / removed, cards played; the card plays by time with the board (`own` / `enemy`).
- Verdict lines: the cells of the two players against each other, the pace, the mines hit, a client result that differs
  from the record (a bug of the writer or of the record - check both).
- `--last N`: the cards played (plays, participants, how many of them won), the cards in the decks of the matches with a
  winner (bots included) and the players. A win rate of a card stands on as many participants or decks as its row says:
  name that number, do not call a card strong on a few matches.

## Tests

```
python3 -m unittest discover -s .claude/skills/match-history/tests
```

On recorded answers of the API (`tests/fixtures`), no network. The client code (`reuse.py`: config, the GET client, the
scrubber) is imported from `client-telemetry`, a link into the framework: a change there is checked by both test suites.
