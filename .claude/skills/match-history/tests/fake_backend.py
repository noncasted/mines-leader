# The reading API of the match history (backend/Orchestration/MetaGateway/Matches/CONTRACT.md) on a local port, answering the
# recorded answers of fixtures/ by path. The list filters as the backend does for the fields a test asks.
import json
import threading
import urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

PREFIX = "/api/match-history"
FIXTURES = Path(__file__).resolve().parent / "fixtures"
M1 = "11111111-0000-4000-8000-000000000001"
M2 = "22222222-0000-4000-8000-000000000002"
ALICE = "aaaaaaaa-0000-4000-8000-00000000000a"
BOB = "bbbbbbbb-0000-4000-8000-00000000000b"
CARL = "cccccccc-0000-4000-8000-00000000000c"


def fixture(name):
    path = FIXTURES / name

    return path.read_text(encoding="utf-8") if name.endswith(".csv") else json.loads(path.read_text(encoding="utf-8"))


class FakeBackend:
    def __init__(self, token):
        self.token = token
        self.requests = []
        self.authorizations = []
        # (status, body, headers) for every request, when set.
        self.forced = None
        self.routes = {
            f"/matches/{M1}": fixture("match_1.json"),
            f"/matches/{M2}": fixture("match_2.json"),
            f"/matches/{M1}/players": fixture("players_1.json"),
            f"/matches/{M2}/players": fixture("players_2.json"),
            f"/matches/{M1}/players/{ALICE}/series": fixture("series_1_alice.json"),
            f"/matches/{M1}/players/{ALICE}/series.csv": fixture("series_1_alice.csv"),
            "/players/carl": fixture("user_carl.json"),
            f"/players/{CARL}": fixture("user_carl.json"),
        }
        self.rows = fixture("matches.json")
        self._server = ThreadingHTTPServer(("127.0.0.1", 0), self._handler())
        self.url = f"http://127.0.0.1:{self._server.server_port}"
        threading.Thread(target=self._server.serve_forever, daemon=True).start()

    def close(self):
        self._server.shutdown()
        self._server.server_close()

    def queries(self, path):
        return [urllib.parse.parse_qs(urllib.parse.urlparse(request).query) for request in self.requests
                if urllib.parse.urlparse(request).path == PREFIX + path]

    def _answer(self, path, query):
        if self.forced:
            return self.forced

        if not path.startswith(PREFIX):
            return 404, "", {}

        path = path[len(PREFIX):]

        if path == "/matches":
            return 200, self._list(query), {}

        if path.startswith("/players/"):
            path = path.lower()

        found = self.routes.get(path)

        return (200, found, {}) if found is not None else (404, "", {})

    def _list(self, query):
        rows = [row for row in self.rows
                if query.get("type") in (None, row["type"])
                and query.get("winner") in (None, "true" if row["winner"] else "false")
                and query.get("closed") in (None, "true" if row["closedAt"] else "false")
                and query.get("player") in (None, *(seat["nickname"] for seat in row["players"]), *(seat["userId"] for seat in row["players"]))]
        offset = int(query.get("offset", "0"))

        return rows[offset:offset + int(query.get("limit", "50"))]

    def _handler(self):
        backend = self

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                parsed = urllib.parse.urlparse(self.path)
                query = dict(urllib.parse.parse_qsl(parsed.query))
                authorization = self.headers.get("Authorization")
                backend.requests.append(self.path)
                backend.authorizations.append(authorization)

                if authorization != f"Bearer {backend.token}":
                    self._send(401, "", {})
                else:
                    self._send(*backend._answer(urllib.parse.unquote(parsed.path), query))

            def _send(self, status, body, headers):
                data = (body if isinstance(body, str) else json.dumps(body)).encode()
                self.send_response(status)

                for name, value in headers.items():
                    self.send_header(name, value)

                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)

            def log_message(self, *arguments):
                pass

        return Handler
