# What follows by numbers from the players of a match and of several: the pace of the turns, where the cells came from, the
# cards against the outcome. The conclusions are the agent's; a number stands on as many matches and plays as it says.
from reuse import table
from history_report import clock, field, is_live, number, percent

# turn.end / card.play / match.end of MatchGameMetrics.
_TURN_END = "turn.end"
_CARD_PLAY = "card.play"
_MATCH_END = "match.end"


class PlayerStats:
    def __init__(self, player):
        self.nickname = player["nickname"] or player["userId"]
        self.user_id = player["userId"]
        self.is_winner = player["isWinner"]
        self.has_metrics = player["streams"] > 0
        self.cells = player["cellsOpened"]
        self.by_source = player["cellsBySource"]
        self.counters = player["counters"]
        self.cards = player["cardsByType"]
        self.cards_played = player["cardsPlayed"]
        events = player["events"]
        self.turn_seconds = [value for value in (field(event, "seconds") for event in events if event["name"] == _TURN_END)
                             if isinstance(value, float)]
        self.result = next((field(event, "result") for event in events if event["name"] == _MATCH_END), None)
        self.plays = [event for event in events if event["name"] == _CARD_PLAY]

    @property
    def turns(self):
        return self.counters.get("turns", 0) or len(self.turn_seconds)

    @property
    def average_turn(self):
        return sum(self.turn_seconds) / len(self.turn_seconds) if self.turn_seconds else None

    @property
    def cells_per_turn(self):
        return self.cells / self.turns if self.turns else None


class MatchAnalysis:
    def __init__(self, row, players):
        self.row = row
        self.players = [PlayerStats(player) for player in players]

    @property
    def measured(self):
        return [player for player in self.players if player.has_metrics]


def match_lines(analysis):
    row = analysis.row
    duration = clock(row["durationSeconds"]) if row.get("durationSeconds") is not None else "live"
    lines = [f"match {row['id']} type={row['type']} duration={duration}"]
    rows = [["player", "won", "result", "turns", "turn s", "cells", "cells/turn", "click", "cascade", "card", "mines", "flags +/-", "cards"]]

    for player in analysis.players:
        if not player.has_metrics:
            lines.append(f"  {player.nickname}: no metrics (streams 0), left out")
            continue

        rows.append([player.nickname, "yes" if player.is_winner else "no", player.result or "-", number(player.turns),
                     _maybe(player.average_turn), number(player.cells), _maybe(player.cells_per_turn),
                     *(percent(player.by_source.get(source, 0), player.cells) for source in ("click", "cascade", "card")),
                     number(player.counters.get("mines.hit", 0)),
                     f"{number(player.counters.get('flags.set', 0))}/{number(player.counters.get('flags.removed', 0))}",
                     number(player.cards_played)])

    if len(rows) > 1:
        lines += [f"  {line}" for line in table(rows)]

    for player in analysis.measured:
        if player.plays:
            plays = " ".join(f"{clock(event['time'])} {field(event, 'card') or '-'}>{field(event, 'board') or '-'}" for event in player.plays)
            lines.append(f"  {player.nickname} cards: {plays}")

    lines += _verdict(analysis)

    return lines


def aggregate_lines(analyses):
    lines = [f"over {len(analyses)} matches"]
    cards = {}
    decks = {}
    people = {}

    for analysis in analyses:
        for player in analysis.measured:
            for card, count in player.cards.items():
                entry = cards.setdefault(card, {"plays": 0, "matches": 0, "won": 0})
                entry["plays"] += count
                entry["matches"] += 1
                entry["won"] += 1 if player.is_winner else 0

            person = people.setdefault(player.nickname, {"matches": 0, "won": 0, "turns": [], "cells": 0, "turn_count": 0})
            person["matches"] += 1
            person["won"] += 1 if player.is_winner else 0
            person["turns"] += player.turn_seconds
            person["cells"] += player.cells
            person["turn_count"] += player.turns

        if analysis.row.get("winner") is None:
            continue

        for seat in analysis.row["players"]:
            for card in set(seat["deck"]):
                entry = decks.setdefault(card, {"decks": 0, "won": 0})
                entry["decks"] += 1
                entry["won"] += 1 if seat["isWinner"] else 0

    if cards:
        rows = [["card played", "plays", "participants", "won", "win rate"]]
        rows += [[card or "-", number(entry["plays"]), str(entry["matches"]), str(entry["won"]), percent(entry["won"], entry["matches"])]
                 for card, entry in sorted(cards.items(), key=lambda item: (-item[1]["plays"], item[0]))]
        lines += ["cards played by the players with metrics (a participant of a match counts once):"] + [f"  {line}" for line in table(rows)]

    if decks:
        rows = [["card in deck", "decks", "won", "win rate"]]
        rows += [[card, str(entry["decks"]), str(entry["won"]), percent(entry["won"], entry["decks"])]
                 for card, entry in sorted(decks.items(), key=lambda item: (-item[1]["decks"], item[0]))]
        lines += ["decks of the matches with a winner (bots included, a card counts once per deck):"] + [f"  {line}" for line in table(rows)]

    if people:
        rows = [["player", "matches", "won", "turn s", "cells/turn"]]
        rows += [[name, str(entry["matches"]), str(entry["won"]),
                  _maybe(sum(entry["turns"]) / len(entry["turns"]) if entry["turns"] else None),
                  _maybe(entry["cells"] / entry["turn_count"] if entry["turn_count"] else None)]
                 for name, entry in sorted(people.items(), key=lambda item: (-item[1]["matches"], item[0]))]
        lines += ["players with metrics:"] + [f"  {line}" for line in table(rows)]

    return lines


def _verdict(analysis):
    measured = analysis.measured

    if not measured:
        return ["  verdict: no participant has metrics"]

    lines = []
    winners = [player for player in measured if player.is_winner]

    if is_live(analysis.row):
        lines.append("  verdict: the match runs, the numbers are of so far")
    elif analysis.row.get("winner") is None:
        lines.append("  verdict: closed without a winner")
    elif not winners:
        lines.append("  verdict: the winner has no metrics")

    if len(measured) == 2:
        first, second = sorted(measured, key=lambda player: -player.cells)

        if second.cells:
            lines.append(f"  cells: {first.nickname} {number(first.cells)} vs {second.nickname} {number(second.cells)} (x{first.cells / second.cells:.2f})")

        paces = [player for player in measured if player.average_turn is not None]

        if len(paces) == 2:
            fast, slow = sorted(paces, key=lambda player: player.average_turn)
            lines.append(f"  pace: {fast.nickname} {fast.average_turn:.1f} s a turn vs {slow.nickname} {slow.average_turn:.1f} s")

    for player in measured:
        if player.counters.get("mines.hit", 0):
            lines.append(f"  {player.nickname} hit {number(player.counters['mines.hit'])} mine(s)")

        if player.result and not is_live(analysis.row) and (player.result == "win") != player.is_winner:
            lines.append(f"  {player.nickname}: the client says {player.result}, the record says winner={'yes' if player.is_winner else 'no'}")

    return lines


def _maybe(value):
    return "-" if value is None else f"{value:.1f}"
