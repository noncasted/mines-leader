# The subcommands. They print what the backend holds and what follows from it by numbers; the conclusions are the agent's.
from pathlib import Path

import history_analysis
import history_report
from reuse import SERVICE, USAGE, Failure

YES_NO = {"yes": "true", "no": "false"}


def matches(api, arguments):
    rows = api.all_matches(arguments.limit, **_filters(arguments))
    print("\n".join(history_report.rows_lines(rows, "matches")))


def match(api, arguments):
    lines = history_report.card_lines(api.match(arguments.id))

    for player in api.players(arguments.id):
        lines += history_report.player_lines(player)

    print("\n".join(lines))


def player(api, arguments):
    print("\n".join(history_report.user_lines(api.player(arguments.player, arguments.limit))))


def series(api, arguments):
    answer = api.series(arguments.id, arguments.user, arguments.step, arguments.axis)
    print("\n".join(history_report.series_lines(answer)))


def export(api, arguments):
    text = api.series_csv(arguments.id, arguments.user, arguments.step, arguments.axis)
    lines = text.splitlines()

    if not lines or lines[0] != "time,series,value":
        raise Failure(SERVICE, "the backend answered a csv without its header time,series,value")

    out = Path(arguments.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(text, encoding="utf-8", newline="\n")
    keys = {line.split(",")[1] for line in lines[1:]}

    print(f"wrote {out}: {len(lines) - 1} points of {len(keys)} series, step {arguments.step} s, axis {arguments.axis}")


def analyze(api, arguments):
    if arguments.id:
        ids = [arguments.id]
    else:
        ids = [row["id"] for row in api.all_matches(arguments.last, **{**_filters(arguments), "closed": "true"})]

        if not ids:
            raise Failure(USAGE, "no closed match fits the filters")

    analyses = []

    for match_id in ids:
        analysis = history_analysis.MatchAnalysis(api.match(match_id), api.players(match_id))
        analyses.append(analysis)
        print("\n".join(history_analysis.match_lines(analysis)))
        print()

    if len(analyses) > 1:
        print("\n".join(history_analysis.aggregate_lines(analyses)))


def _filters(arguments):
    return {"player": arguments.player, "type": arguments.type,
            "winner": YES_NO.get(arguments.winner), "closed": YES_NO.get(arguments.closed),
            "from": arguments.since, "to": arguments.until}
