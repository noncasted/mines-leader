# The compose of a local run (local_compose.py) from a small game compose:
#   python3 -m unittest discover -s backend/measurements/tools/tests
import copy
import unittest

import support  # noqa: F401  first: it puts the tools on the path

import local_compose  # noqa: E402

ENV = {"DB_HOST": "postgres", "DB_PORT": "5432", "DB_NAME": "measurements", "DB_USER": "measurements",
       "DB_PASSWORD": "pw"}

GAME = {
    "name": "game",
    "networks": {"game": {"name": "game", "external": True}},
    "volumes": {"aspire-dashboard-keys": {"name": "game_aspire-dashboard-keys"}, "console-keys": {"name": "game_console-keys"}},
    "services": {
        "pgbouncer": {"image": "edoburu/pgbouncer", "environment": {"DB_HOST": "postgres"}, "networks": {"game": None},
                      "healthcheck": {"test": ["CMD", "true"], "interval": "2s", "retries": 30, "start_period": "5s"}},
        "backend": {
            "build": {"context": "/src", "dockerfile": "Dockerfile", "target": "monolith"},
            "image": "game/monolith:latest",
            "environment": {"DOTNET_gcServer": "0", "OTEL_EXPORTER_OTLP_ENDPOINT": "http://aspire-dashboard:18889",
                            "OTEL_EXPORTER_OTLP_PROTOCOL": "grpc", "OTEL_EXPORTER_OTLP_HEADERS": "x-otlp-api-key=k"},
            "ports": ["7100:8080"],
            "labels": ["traefik.enable=true"],
            "depends_on": {"pgbouncer": {"condition": "service_healthy"}, "aspire-dashboard": {"condition": "service_started"}},
            "networks": {"game": None},
        },
        "aspire-dashboard": {"image": "mcr.microsoft.com/dotnet/aspire-dashboard:13.6", "networks": {"game": None}},
    },
}

STACK = {
    "name": "observability",
    "networks": {"observability": {"name": "observability", "external": True}},
    "volumes": {"victorialogs-data": {"name": "observability_victorialogs-data"}},
    "services": {
        "victorialogs": {"image": "victoriametrics/victoria-logs:v1.54.0", "ports": ["127.0.0.1:9428:9428"],
                         "networks": {"observability": None}, "mem_limit": "256m"},
        "otlp": {"image": "nginx:1.30.8-alpine-slim", "networks": {"observability": None}},
    },
}


# The same game exporting into the stack of the host, as the compose of the deploy branch after the dashboard.
VICTORIA_GAME = copy.deepcopy(GAME)
VICTORIA_GAME["networks"]["observability"] = {"name": "observability", "external": True}
del VICTORIA_GAME["volumes"]["aspire-dashboard-keys"]
del VICTORIA_GAME["services"]["aspire-dashboard"]
del VICTORIA_GAME["services"]["backend"]["depends_on"]["aspire-dashboard"]
VICTORIA_GAME["services"]["backend"]["networks"] = {"game": {"gw_priority": 100}, "observability": None}


def transform(gc="workstation", game=GAME, stack=None):
    return local_compose.transform(copy.deepcopy(game), run="measure-run", namespace="game", gc=gc, cpus=6.0,
                                   postgres_image="postgres:18-alpine", env=ENV, stack=copy.deepcopy(stack))


class Variables(unittest.TestCase):
    def test_only_the_required_ones_without_comments(self):
        text = "# ${NAME:?} in a comment\n  a: ${DB_USER:?}\n  b: ${OPTIONAL:-x}\n  c: ${PLAIN}\n  d: x-${OTEL_API_KEY:?}"

        self.assertEqual(["DB_USER", "OTEL_API_KEY"], local_compose.required_variables(text))

    def test_the_database_of_the_run_and_throwaway_values(self):
        values = local_compose.env_values(["DB_HOST", "GAME_SERVER_URL", "CORS_ALLOWED_ORIGINS", "A", "B"])

        self.assertEqual("postgres", values["DB_HOST"])
        self.assertEqual("measurements", values["DB_USER"])
        self.assertEqual("http://localhost:8080", values["GAME_SERVER_URL"])
        self.assertEqual("http://localhost", values["CORS_ALLOWED_ORIGINS"])
        self.assertEqual(64, len(values["A"]))
        self.assertNotEqual(values["A"], values["B"])
        self.assertNotEqual(values["A"], values["DB_PASSWORD"])


class Transform(unittest.TestCase):
    def test_nothing_is_shared_with_the_host(self):
        config = transform()

        self.assertEqual("measure-run", config["name"])
        self.assertEqual({"game": {"name": "measure-run_game", "driver": "bridge"}}, config["networks"])
        self.assertEqual({"measure-run_aspire-dashboard-keys", "measure-run_console-keys", "measure-run_postgres-data"},
                         {v["name"] for v in config["volumes"].values()})
        for name, service in config["services"].items():
            self.assertNotIn("ports", service)
            self.assertEqual(f"{name}-measure-run", service["container_name"])
            self.assertEqual(6.0, service["cpus"])
            self.assertEqual("game", service["labels"]["coolify.projectName"])

    def test_a_slow_start_is_not_unhealthy(self):
        healthcheck = transform()["services"]["pgbouncer"]["healthcheck"]

        self.assertEqual({"test": ["CMD", "true"], "interval": "2s", "retries": 30, "start_period": "600s"}, healthcheck)

    def test_built_images_get_their_own_tags_and_the_host_network(self):
        backend = transform()["services"]["backend"]

        self.assertEqual("backend-measurements/backend:local", backend["image"])
        self.assertEqual("host", backend["build"]["network"])
        self.assertEqual("true", backend["labels"]["traefik.enable"])

    def test_own_postgres_and_who_waits_for_it(self):
        services = transform()["services"]

        self.assertEqual("postgres:18-alpine", services["postgres"]["image"])
        self.assertEqual("pw", services["postgres"]["environment"]["POSTGRES_PASSWORD"])
        self.assertEqual({"game": None}, services["postgres"]["networks"])
        self.assertEqual("service_healthy", services["pgbouncer"]["depends_on"]["postgres"]["condition"])
        self.assertNotIn("postgres", services["backend"]["depends_on"])

    def test_gc_variants(self):
        def gc_of(gc):
            environment = transform(gc=gc)["services"]["backend"]["environment"]
            return {k: v for k, v in environment.items() if k.startswith("DOTNET_")}

        self.assertEqual({}, gc_of("server"))
        self.assertEqual({"DOTNET_gcServer": "0"}, gc_of("workstation"))
        self.assertEqual({"DOTNET_gcServer": "0", "DOTNET_GCConserveMemory": "5"}, gc_of("conserve"))
        self.assertNotIn("DOTNET_gcServer", transform(gc="conserve")["services"]["aspire-dashboard"].get("environment", {}))

    def test_aspire_keeps_the_dashboard(self):
        services = transform()["services"]

        self.assertIn("aspire-dashboard", services)
        self.assertEqual("http://aspire-dashboard:18889", services["backend"]["environment"]["OTEL_EXPORTER_OTLP_ENDPOINT"])

    def test_the_telemetry_of_the_compose(self):
        self.assertEqual("aspire", local_compose.telemetry_of(GAME))
        self.assertEqual("victoria", local_compose.telemetry_of(VICTORIA_GAME))

        both = copy.deepcopy(VICTORIA_GAME)
        both["services"]["aspire-dashboard"] = GAME["services"]["aspire-dashboard"]
        with self.assertRaisesRegex(ValueError, "either aspire-dashboard or the network observability"):
            local_compose.telemetry_of(both)

    def test_victoria_runs_the_stack_of_the_host_in_the_run(self):
        config = transform(game=VICTORIA_GAME, stack=STACK)
        services = config["services"]

        self.assertEqual({"game": {"name": "measure-run_game", "driver": "bridge"},
                          "observability": {"name": "measure-run_observability", "driver": "bridge"}}, config["networks"])
        self.assertEqual({"observability": None}, services["victorialogs"]["networks"])
        self.assertEqual({"game": None}, services["postgres"]["networks"])
        self.assertEqual("measure-run_victorialogs-data", config["volumes"]["victorialogs-data"]["name"])
        self.assertIn("otlp", services)
        self.assertNotIn("ports", services["victorialogs"])
        self.assertEqual("256m", services["victorialogs"]["mem_limit"])
        self.assertEqual("victorialogs-measure-run", services["victorialogs"]["container_name"])

    def test_wrong_input_fails(self):
        with self.assertRaisesRegex(ValueError, "unknown GC"):
            transform(gc="fast")
        with self.assertRaisesRegex(ValueError, "exactly when the game exports into it"):
            transform(game=VICTORIA_GAME)
        with self.assertRaisesRegex(ValueError, "exactly when the game exports into it"):
            transform(stack=STACK)

        game = copy.deepcopy(GAME)
        game["services"]["postgres"] = {"image": "postgres"}
        with self.assertRaisesRegex(ValueError, "already has a service postgres"):
            local_compose.transform(game, run="r", namespace="g", gc="server", cpus=1,
                                    postgres_image="p", env=ENV)


if __name__ == "__main__":
    unittest.main()
