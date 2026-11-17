# The script against a local fake of the reading API on recorded answers (fixtures/), as a real process, no network:
#   python3 -m unittest discover -s .claude/skills/match-history/tests
import os
import subprocess
import sys
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from fake_backend import ALICE, BOB, CARL, M1, M2, FakeBackend, fixture  # noqa: E402

SKILL = Path(__file__).resolve().parent.parent
SCRIPT = SKILL / "history.py"
TOKEN = "s3cr3t-Token-value-123"
READY, API, CONFIG, BAD_AUTH, MISSING = 0, 1, 2, 3, 4


class Base(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.temp = Path(temp.name)
        self.backend = FakeBackend(TOKEN)
        self.addCleanup(self.backend.close)
        self.env_file = self.temp / "deploy.env"
        self.env_file.write_text(f"OTHER_SECRET=other-value\nCONSOLE_TOKEN={TOKEN}\n")

    def run_bare(self, *arguments, env=None):
        base = {"PATH": os.environ["PATH"], "HOME": str(self.temp)}
        result = subprocess.run([sys.executable, str(SCRIPT), *arguments], capture_output=True, text=True, timeout=60, env={**base, **(env or {})})
        self.assertNotIn(TOKEN, result.stdout + result.stderr)
        self.assertNotIn("other-value", result.stdout + result.stderr)
        self.assertNotIn("Traceback", result.stderr)

        return result

    def run_cli(self, *arguments, url=None):
        return self.run_bare(*arguments, "--url", url or self.backend.url, "--env-file", str(self.env_file))

    def ok(self, *arguments):
        result = self.run_cli(*arguments)
        self.assertEqual(READY, result.returncode, result.stderr)

        return result.stdout


class LegacyRecordTests(unittest.TestCase):
    # A record completed before MatchClosed was kept: a winner, no closedAt, no nicknames.
    def test_a_completed_record_without_closed_at_is_not_live(self):
        sys.path.insert(0, str(SKILL))
        from history_report import winner_name

        seats = [{"userId": "u-1", "nickname": "", "isWinner": True, "ratingChange": 25},
                 {"userId": "u-2", "nickname": "", "isWinner": False, "ratingChange": -15}]

        self.assertEqual("u-1", winner_name({"closedAt": None, "winner": "u-1", "players": seats}))
        self.assertEqual("live", winner_name({"closedAt": None, "winner": None, "players": seats}))


class CommandTests(Base):
    def test_matches_with_the_filters(self):
        out = self.ok("matches", "--player", "Alice", "--type", "LastManStandingTurnBased", "--winner", "yes", "--closed", "yes",
                      "--from", "2026-11-17T00:00:00Z", "--limit", "5")

        self.assertIn(f"{M1}  2026-11-17 12:00:00  type=LastManStandingTurnBased duration=3:00 winner=Alice players=Alice(+12),Bob(-12)", out)
        self.assertNotIn(M2, out)
        self.assertEqual({"player": ["Alice"], "type": ["LastManStandingTurnBased"], "winner": ["true"], "closed": ["true"],
                          "from": ["2026-11-17T00:00:00Z"], "offset": ["0"], "limit": ["5"]}, self.backend.queries("/matches")[0])

    def test_a_match_without_a_winner(self):
        self.assertIn(f"{M2}  2026-11-17 13:00:00  type=LastManStandingTurnBased duration=1:30 winner=none", self.ok("matches"))

    def test_unknown_type_is_refused_before_the_backend(self):
        self.assertEqual(CONFIG, self.run_cli("matches", "--type", "Run").returncode)
        self.assertEqual([], self.backend.requests)

    def test_match_shows_the_decks_the_cells_the_turns_and_the_cards(self):
        out = self.ok("match", M1)

        self.assertIn(f"participant Alice {ALICE} winner=yes rating=+12 deck=Trebuchet_Max,Smoke,Medic", out)
        self.assertIn("cells=60 (cascade 30 (50%), click 20 (33%), card 10 (17%)) cascades=4 flags.set=6 flags.removed=1 mines.hit=0 turns=3", out)
        self.assertIn("cards=2: smoke 1, trebuchet_max 1", out)
        self.assertIn("0:50  2     30       25     1", out)
        self.assertIn("0:45  trebuchet_max  enemy  9", out)
        # card.play without cells: the client did not know them.
        self.assertIn("2:10  smoke          own    -", out)
        self.assertIn("match.end at 3:00: result=win seconds=180", out)
        self.assertIn(f"player Bob {BOB} winner=no rating=-12 streams=1", out)

    def test_match_of_players_without_metrics(self):
        out = self.ok("match", M2)

        self.assertEqual(2, out.count("no metrics"))

    def test_player_by_nickname_or_id(self):
        self.assertIn(f"user Carl {CARL} matches=1", self.ok("player", "Carl"))
        self.assertIn(M2, self.ok("player", CARL))
        self.assertEqual(["20"], self.backend.queries(f"/players/{CARL}")[0]["limit"])

    def test_unknown_match_is_not_found(self):
        result = self.run_cli("match", "99999999-0000-4000-8000-000000000009")

        self.assertEqual(MISSING, result.returncode)
        self.assertIn("404", result.stderr)

    def test_ambiguous_name_shows_the_ids_of_the_backend(self):
        self.backend.forced = (409, "nickname 'Sam' belongs to users 1111, 2222", {})
        result = self.run_cli("player", "Sam")

        self.assertEqual(API, result.returncode)
        self.assertIn("nickname 'Sam' belongs to users 1111, 2222", result.stderr)


class SeriesTests(Base):
    def test_series_as_a_table_with_totals(self):
        out = self.ok("series", M1, ALICE, "--step", "10")

        self.assertIn("time   cells.opened:click  cells.opened:cascade  cards.played:trebuchet_max", out)
        self.assertIn("0:40   15                  20                    1", out)
        self.assertIn("total  20                  30                    1", out)
        self.assertEqual({"step": ["10"], "axis": ["run"]}, self.backend.queries(f"/matches/{M1}/players/{ALICE}/series")[0])

    def test_step_out_of_the_contract_is_refused(self):
        self.assertEqual(CONFIG, self.run_cli("series", M1, ALICE, "--step", "5").returncode)

    def test_export_writes_the_csv_as_it_came(self):
        out_file = self.temp / "out" / "alice.csv"
        out = self.ok("export", M1, ALICE, "--axis", "server", "--out", str(out_file))

        self.assertEqual(fixture("series_1_alice.csv"), out_file.read_text(encoding="utf-8"))
        self.assertIn("5 points of 3 series, step 10 s, axis server", out)
        self.assertEqual({"step": ["10"], "axis": ["server"]}, self.backend.queries(f"/matches/{M1}/players/{ALICE}/series.csv")[0])

    def test_a_participant_unknown_to_the_match(self):
        self.assertEqual(MISSING, self.run_cli("series", M1, CARL).returncode)


class AnalyzeTests(Base):
    def test_one_match(self):
        out = self.ok("analyze", M1)

        self.assertIn("Alice   yes  win     3      25.0    60     20.0        33%    50%      17%   0      6/1        2", out)
        self.assertIn("Bob     no   lose    3      50.0    30     10.0        83%    17%      0%    1      2/0        1", out)
        self.assertIn("Alice cards: 0:45 trebuchet_max>enemy 2:10 smoke>own", out)
        self.assertIn("cells: Alice 60 vs Bob 30 (x2.00)", out)
        self.assertIn("pace: Alice 25.0 s a turn vs Bob 50.0 s", out)
        self.assertIn("Bob hit 1 mine(s)", out)

    def test_last_matches_are_closed_and_summed(self):
        out = self.ok("analyze", "--last", "2", "--closed", "no")

        self.assertEqual(["true"], self.backend.queries("/matches")[0]["closed"])
        self.assertIn("Carl: no metrics (streams 0), left out", out)
        self.assertIn("over 2 matches", out)
        self.assertIn("smoke          2      2             1    50%", out)
        # The match without a winner is out of the decks.
        self.assertIn("Medic          1      1    100%", out)
        self.assertIn("ZipZap         1      0    0%", out)

    def test_a_client_result_against_the_record(self):
        players = fixture("players_1.json")
        players[1]["events"][-1]["fields"]["result"] = "win"
        self.backend.routes[f"/matches/{M1}/players"] = players

        self.assertIn("Bob: the client says win, the record says winner=no", self.ok("analyze", M1))

    def test_filters_need_last(self):
        self.assertEqual(CONFIG, self.run_cli("analyze", M1, "--winner", "yes").returncode)


class SafetyTests(Base):
    def test_token_only_in_the_header(self):
        self.ok("analyze", M1)

        self.assertTrue(all(value == f"Bearer {TOKEN}" for value in self.backend.authorizations))
        self.assertTrue(all(TOKEN not in request for request in self.backend.requests))

    def test_token_echoed_by_the_backend_is_cut(self):
        self.backend.forced = (400, f"bad query with {TOKEN}", {})
        result = self.run_cli("matches")

        self.assertEqual(API, result.returncode)
        self.assertIn("***", result.stderr)

    def test_wrong_token(self):
        self.env_file.write_text("CONSOLE_TOKEN=wrong-token-value\n")
        result = self.run_cli("matches")

        self.assertEqual(BAD_AUTH, result.returncode)
        self.assertNotIn("wrong-token-value", result.stderr)

    def test_plain_http_to_another_host_is_refused(self):
        result = self.run_cli("matches", url="http://meta.example.com")

        self.assertEqual(CONFIG, result.returncode)
        self.assertIn("must be https", result.stderr)
        self.assertEqual([], self.backend.requests)

    def test_redirect_is_not_followed(self):
        other = _Recorder()
        self.addCleanup(other.close)
        self.backend.forced = (302, "", {"Location": f"{other.url}/api/match-history/matches"})
        result = self.run_cli("matches")

        self.assertEqual(API, result.returncode)
        self.assertIn("redirect", result.stderr)
        self.assertEqual([], other.authorizations)

    def test_section_of_the_project_gives_the_address(self):
        project = self.temp / "project" / ".claude"
        project.mkdir(parents=True)
        (project / "project-skills.md").write_text(f"## client-telemetry\n\n- url: `{self.backend.url}`\n- env-file: `{self.env_file}`\n")
        result = self.run_bare("matches", env={"CLAUDE_PROJECT_DIR": str(self.temp / "project")})

        self.assertEqual(READY, result.returncode, result.stderr)
        self.assertEqual(f"Bearer {TOKEN}", self.backend.authorizations[0])


class _Recorder:
    # Another host a redirect would name: it must never see the header.
    def __init__(self):
        self.authorizations = []
        recorder = self

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                recorder.authorizations.append(self.headers.get("Authorization"))
                self.send_response(200)
                self.send_header("Content-Length", "2")
                self.end_headers()
                self.wfile.write(b"[]")

            def log_message(self, *arguments):
                pass

        self._server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.url = f"http://localhost:{self._server.server_port}"
        threading.Thread(target=self._server.serve_forever, daemon=True).start()

    def close(self):
        self._server.shutdown()
        self._server.server_close()


if __name__ == "__main__":
    unittest.main()
