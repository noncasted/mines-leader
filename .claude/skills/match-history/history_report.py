# The text of the lists, of one match and of the series: what the backend holds, as it is (the card names are CardType: the
# deck by its C# name, the tag card of the metrics in lower case).
from datetime import datetime, timezone

from reuse import table

# Meta.Matches.MatchGameMetrics.PlainCounters (backend/Meta/Matches/Players): the counters of a player without a tag.
PLAIN_COUNTERS = ("cascades", "flags.set", "flags.removed", "mines.hit", "turns")


def clock(seconds):
    if seconds is None:
        return "-"

    seconds = int(round(seconds))

    return f"{seconds // 60}:{seconds % 60:02d}"


def number(value):
    return f"{value:,.0f}".replace(",", " ") if value == int(value) else f"{value:.2f}"


def percent(part, whole):
    return f"{100 * part / whole:.0f}%" if whole else "-"


def signed(value):
    return f"+{value}" if value > 0 else str(value)


# A record completed before MatchClosed was kept (older than the history) has a winner and no closedAt: it is not live.
def is_live(row):
    return row.get("closedAt") is None and row.get("winner") is None


# Records older than the history have no nicknames: the user id stands for them.
def seat_name(seat):
    return seat["nickname"] or seat["userId"]


def winner_name(row):
    if is_live(row):
        return "live"

    winners = [seat_name(seat) for seat in row["players"] if seat["isWinner"]]

    return winners[0] if winners else "none"


def row_line(row):
    players = ",".join(f"{seat_name(seat)}({signed(seat['ratingChange'])})" for seat in row["players"]) or "-"
    duration = clock(row["durationSeconds"]) if row.get("durationSeconds") is not None else "live"

    return (f"{row['id']}  {time(row['startedAt'])}  type={row['type']} duration={duration} "
            f"winner={winner_name(row)} players={players}")


def rows_lines(rows, title):
    return [f"{title}: {len(rows)} (newest first)"] + [row_line(row) for row in rows]


def card_lines(row):
    duration = clock(row["durationSeconds"]) if row.get("durationSeconds") is not None else "live"
    lines = [f"match {row['id']}",
             f"  type={row['type']} duration={duration} winner={winner_name(row)}",
             f"  started={time(row['startedAt'])} closed={time(row.get('closedAt'))}"]

    for seat in row["players"]:
        lines.append(f"  participant {seat_name(seat)} {seat['userId']} winner={'yes' if seat['isWinner'] else 'no'} "
                     f"rating={signed(seat['ratingChange'])} deck={','.join(seat['deck']) or '-'}")

    return lines


def player_lines(player):
    lines = [f"player {player['nickname']} {player['userId']} winner={'yes' if player['isWinner'] else 'no'} "
             f"rating={signed(player['ratingChange'])} streams={player['streams']}"]

    if not player["streams"]:
        return lines + ["  no metrics: the client of this participant wrote nothing (a bot or an old client)"]

    cells = player["cellsOpened"]
    sources = player["cellsBySource"]
    by_source = ", ".join(f"{name or '-'} {number(value)} ({percent(value, cells)})" for name, value in _ordered(sources))
    counters = " ".join(f"{name}={number(player['counters'].get(name, 0))}" for name in PLAIN_COUNTERS)
    lines.append(f"  cells={number(cells)} ({by_source or '-'}) {counters}")
    cards = ", ".join(f"{name or '-'} {number(value)}" for name, value in _ordered(player["cardsByType"]))
    lines.append(f"  cards={number(player['cardsPlayed'])}: {cards or '-'}")
    events = sorted(player["events"], key=lambda event: event["time"])
    turns = [event for event in events if event["name"] == "turn.end"]
    plays = [event for event in events if event["name"] == "card.play"]

    if turns:
        rows = [["time", "turn", "seconds", "cells", "cards"]]
        rows += [[clock(event["time"]), *(_field(event, name) for name in ("number", "seconds", "cells", "cards"))] for event in turns]
        lines.append(f"  turns: {len(turns)}")
        lines += [f"    {line}" for line in table(rows)]

    if plays:
        rows = [["time", "card", "board", "cells"]]
        rows += [[clock(event["time"]), *(_field(event, name) for name in ("card", "board", "cells"))] for event in plays]
        lines.append(f"  card plays: {len(plays)}")
        lines += [f"    {line}" for line in table(rows)]

    for event in events:
        if event["name"] == "match.end":
            lines.append(f"  match.end at {clock(event['time'])}: result={_field(event, 'result')} seconds={_field(event, 'seconds')}")

    return lines


def user_lines(answer):
    return ([f"user {answer['nickname']} {answer['userId']} matches={answer['total']} (shown {len(answer['matches'])}, newest first)"]
            + [row_line(row) for row in answer["matches"]])


def series_lines(answer):
    lines = answer["lines"]
    head = f"series of {answer['userId']} step={answer['step']}s axis={answer['axis']}: {len(lines)} lines"

    if not lines:
        return [head, "  no points: the participant has no metrics, or wrote no counter"]

    keys = [line["key"] for line in lines]
    values = {}

    for line in lines:
        for point in line["points"]:
            values.setdefault(point["time"], {})[line["key"]] = point["value"]

    stamp = clock if answer["axis"] == "run" else _unix
    rows = [["time", *keys]]
    rows += [[stamp(moment), *(number(values[moment][key]) if key in values[moment] else "" for key in keys)] for moment in sorted(values)]
    rows.append(["total", *(number(sum(point["value"] for point in line["points"])) for line in lines)])

    return [head] + [f"  {line}" for line in table(rows)]


def field(event, name):
    value = event["fields"].get(name) if event.get("fields") else None

    if value is None:
        return None

    try:
        return float(value)
    except (TypeError, ValueError):
        return value


def _field(event, name):
    value = field(event, name)

    if value is None:
        return "-"

    return number(value) if isinstance(value, float) else str(value)


def _ordered(values):
    return sorted(values.items(), key=lambda item: (-item[1], item[0]))


def _unix(seconds):
    return datetime.fromtimestamp(seconds, timezone.utc).strftime("%H:%M:%S")


def parse(text):
    return datetime.fromisoformat(text.replace("Z", "+00:00"))


def time(text):
    return parse(text).strftime("%Y-%m-%d %H:%M:%S") if text else "-"
