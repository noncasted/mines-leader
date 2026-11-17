#!/usr/bin/env python3
# The match history of the meta for agents, GET only (SKILL.md). Exit codes: those of client-telemetry (2 usage, 1 service,
# 3 token refused, 4 not found).
#   history matches [filters] [--limit N]
#   history match <id>
#   history player <nickname|id> [--limit N]
#   history series <id> <userId> [--step 1|10|60] [--axis run|server]
#   history export <id> <userId> --out <file.csv> [--step 1|10|60] [--axis run|server]
#   history analyze <id> | --last N [filters]
# filters: --player P --type T --winner yes|no --closed yes|no --from T --to T (ISO 8601 with an offset)
# Every subcommand: [--url U] [--env-file F] over the section ## client-telemetry of .claude/project-skills.md (the same meta and
# token).
import argparse
import re
import sys

import history_commands
from history_api import HistoryApi
from reuse import Failure, Scrubbed, read_token, resolve_settings

_GUID = re.compile(r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
# Shared.GameMatchType: the names the backend takes and answers.
MATCH_TYPES = ("Single", "TimeLimited", "LastManStanding", "LastManStandingTurnBased")
_FILTERS = ("player", "type", "winner", "closed", "since", "until")


def _guid(what):
    def parse(text):
        if not _GUID.match(text):
            raise argparse.ArgumentTypeError(f"{what} is a GUID")

        return text

    return parse


def _count(highest):
    def parse(text):
        value = int(text)

        if not 1 <= value <= highest:
            raise argparse.ArgumentTypeError(f"from 1 to {highest}")

        return value

    return parse


def _series_options(parser):
    parser.add_argument("id", type=_guid("a match id"))
    parser.add_argument("user", type=_guid("a user id"))
    parser.add_argument("--step", type=int, choices=[1, 10, 60], default=10, help="seconds of a step")
    parser.add_argument("--axis", choices=["run", "server"], default="run", help="run - the match clock, server - unix seconds of the server")


def _parser():
    common = argparse.ArgumentParser(add_help=False, allow_abbrev=False)
    common.add_argument("--url")
    common.add_argument("--env-file")

    filters = argparse.ArgumentParser(add_help=False, allow_abbrev=False)
    filters.add_argument("--player", help="a meta id, or a nickname at the setup of a match (case ignored)")
    filters.add_argument("--type", choices=MATCH_TYPES, help="the type of a match")
    filters.add_argument("--winner", choices=sorted(history_commands.YES_NO), help="yes - only matches with a winner, no - without one (running ones too)")
    filters.add_argument("--closed", choices=sorted(history_commands.YES_NO), help="yes - only closed matches, no - only running ones")
    filters.add_argument("--from", dest="since", help="the start of a match from (inclusive), ISO 8601 with an offset")
    filters.add_argument("--to", dest="until", help="the start of a match before (exclusive), ISO 8601 with an offset")

    parser = argparse.ArgumentParser(prog="history", allow_abbrev=False, description="The match history of the meta, read only.")
    commands = parser.add_subparsers(dest="command", required=True)

    def add(name, handler, *parents, help_text):
        sub = commands.add_parser(name, parents=[common, *parents], allow_abbrev=False, help=help_text)
        sub.set_defaults(handler=handler)

        return sub

    add("matches", history_commands.matches, filters, help_text="the last matches, newest first").add_argument(
        "--limit", type=_count(5000), default=20)

    add("match", history_commands.match, help_text="one match: the row, the players, their turns and cards").add_argument(
        "id", type=_guid("a match id"))

    player = add("player", history_commands.player, help_text="the matches of one user")
    player.add_argument("player", help="a meta id or the nickname now")
    player.add_argument("--limit", type=_count(1000), default=20)

    _series_options(add("series", history_commands.series, help_text="the counters of one participant by steps, a table"))

    export = add("export", history_commands.export, help_text="the series of one participant as csv (time,series,value)")
    _series_options(export)
    export.add_argument("--out", required=True)

    analyze = add("analyze", history_commands.analyze, filters, help_text="the players against each other, the cards, a summary")
    target = analyze.add_mutually_exclusive_group(required=True)
    target.add_argument("id", type=_guid("a match id"), nargs="?")
    target.add_argument("--last", type=_count(200), help="the last N closed matches of the filters, with a summary over them")

    return parser


def main():
    arguments = _parser().parse_args()

    if arguments.command == "analyze" and arguments.id and any(getattr(arguments, name, None) for name in _FILTERS):
        print("history: the filters go with --last, not with an id", file=sys.stderr)
        return 2

    try:
        settings = resolve_settings(arguments.url, arguments.env_file)
        token = read_token(settings.env_file)
        sys.stdout = Scrubbed(sys.stdout, token)
        sys.stderr = Scrubbed(sys.stderr, token)
        arguments.handler(HistoryApi(settings.url, token, settings.env_file), arguments)
    except Failure as failure:
        print(f"history: {failure.message}", file=sys.stderr)
        return failure.code

    return 0


if __name__ == "__main__":
    sys.exit(main())
