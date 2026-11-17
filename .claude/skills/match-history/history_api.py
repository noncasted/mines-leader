# GET over the reading API of the match history (backend/Orchestration/MetaGateway/Matches/CONTRACT.md). The request, the
# repeats of a broken connection, the refused redirects and the header of the token are those of the client-telemetry Api.
import json
import urllib.parse

from reuse import NOT_FOUND, SERVICE, Failure, TelemetryApi

PREFIX = "/api/match-history"
_PAGE = 1000


class HistoryApi(TelemetryApi):
    def matches(self, **filters):
        return self._json("/matches", **filters)

    def all_matches(self, limit, **filters):
        rows = []

        while len(rows) < limit:
            asked = min(_PAGE, limit - len(rows))
            page = self.matches(**filters, offset=len(rows), limit=asked)
            rows += page

            if len(page) < asked:
                break

        return rows[:limit]

    def match(self, match_id):
        return self._json(f"/matches/{match_id}")

    def players(self, match_id):
        return self._json(f"/matches/{match_id}/players")

    def series(self, match_id, user_id, step, axis):
        return self._json(f"/matches/{match_id}/players/{user_id}/series", step=step, axis=axis)

    def series_csv(self, match_id, user_id, step, axis):
        return self._text(f"/matches/{match_id}/players/{user_id}/series.csv", step=step, axis=axis)

    def player(self, player, limit):
        return self._json(f"/players/{urllib.parse.quote(player, safe='')}", limit=limit)

    def _json(self, path, **query):
        target, body = self._fetch(path, query)

        try:
            return json.loads(body)
        except ValueError:
            raise Failure(SERVICE, f"the answer of {target} is not JSON: wrong url, or a proxy in the way?") from None

    def _text(self, path, **query):
        target, body = self._fetch(path, query)

        try:
            return body.decode("utf-8")
        except UnicodeDecodeError:
            raise Failure(SERVICE, f"the answer of {target} is not UTF-8 text") from None

    def _fetch(self, path, query):
        given = {name: value for name, value in query.items() if value is not None}
        target = f"{self._url}{PREFIX}{path}"
        full = f"{target}?{urllib.parse.urlencode(given, doseq=True)}" if given else target

        return target, self._read(full, target, 0)

    def _status_failure(self, error, target):
        if error.code == 404:
            return Failure(NOT_FOUND, f"404 from {target}: no such match, user or participant on this host, or the meta does not serve the match history")

        return super()._status_failure(error, target)
