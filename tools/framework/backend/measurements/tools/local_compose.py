#!/usr/bin/env python3
"""The compose of a local memory run (`local.sh`): the deploy compose of a game made runnable on a workstation.

    local_compose.py env <compose file>        prints an env file: the database of the run and a throwaway value
                                               for every required variable (${NAME:?}) of the compose file
    local_compose.py telemetry                 reads the config of the game on stdin, prints aspire or victoria
    local_compose.py transform <args>          reads `docker compose config --format json` of the game on stdin and
                                               prints the compose of the run (JSON, which compose reads as YAML)

The run keeps what decides the memory of the deploy (images, environment, healthchecks, memory limits) and changes the
rest, nothing on the host is shared with another compose project:
- its own Postgres (`--postgres-image`) with a volume of the run; the services that take `DB_HOST` wait for it;
- every network becomes a bridge of the run, every volume a volume of the run, no port is published;
- containers are named `<service>-<run>` like the Coolify ones (`<service>-<uuid>-<time>`), labelled with the Coolify
  project, so `prod_report.py` groups them as on the hosts;
- healthchecks get a long `start_period` (a loaded workstation starts the roles slower than the retries allow);
- every service gets `cpus` of the VPS (Server GC sizes its heaps by the processors it may use);
- images are built over the host network (the Docker bridge of the workstation may have no internet) and tagged
  `backend-measurements/<service>:local`, never the tags of the game;
- the GC variant sets the DOTNET_* variables of the services the run builds;
- the telemetry is the one of the compose: `aspire` runs its own `aspire-dashboard`; `victoria` exports into the stack
  of the host over the external network `observability`, which the run starts itself from `deploy/observability`
  (`--stack`: its `docker compose config --format json`) on a bridge of the run, as Coolify runs it next to the game.
"""

import argparse
import json
import re
import secrets
import sys
from pathlib import Path

GC_VARIANTS = {
    "server": {},
    "workstation": {"DOTNET_gcServer": "0"},
    "conserve": {"DOTNET_gcServer": "0", "DOTNET_GCConserveMemory": "5"},
}
GC_KEYS = ("DOTNET_gcServer", "DOTNET_GCConserveMemory")
STACK_NETWORK = "observability"
DASHBOARD = "aspire-dashboard"
POSTGRES = "postgres"
DATABASE = {"DB_HOST": POSTGRES, "DB_PORT": "5432", "DB_NAME": "measurements", "DB_USER": "measurements"}
# A workstation shared with other work starts the roles slower than a VPS: failed probes during this period do not count,
# so the retries of the deploy do not mark a slow start unhealthy (the run waits for healthy, `local.sh`).
START_PERIOD = "600s"
REQUIRED = re.compile(r"\$\{([A-Za-z_][A-Za-z0-9_]*):\?")


def required_variables(text):
    """The ${NAME:?} variables of a compose file, comments left out."""
    lines = [line for line in text.splitlines() if not line.lstrip().startswith("#")]
    return sorted(set(REQUIRED.findall("\n".join(lines))))


def env_values(names):
    """The database of the run and a throwaway value for every other required variable (64 hex digits: a secret
    read as base64 still has the 32 bytes the game requires)."""
    values = dict(DATABASE, DB_PASSWORD=secrets.token_hex(16))

    for name in names:
        if name in values:
            continue
        if name.endswith("_URL"):
            values[name] = "http://localhost:8080"
        elif name.endswith("ORIGINS"):
            values[name] = "http://localhost"
        else:
            values[name] = secrets.token_hex(32)

    return values


def postgres_service(image, env, network):
    return {
        "image": image,
        "environment": {
            "POSTGRES_USER": env["DB_USER"],
            "POSTGRES_PASSWORD": env["DB_PASSWORD"],
            "POSTGRES_DB": env["DB_NAME"],
        },
        "volumes": [{"type": "volume", "source": "postgres-data", "target": "/var/lib/postgresql"}],
        "healthcheck": {
            "test": ["CMD-SHELL", f"pg_isready -U {env['DB_USER']} -d {env['DB_NAME']}"],
            "interval": "2s",
            "timeout": "3s",
            "retries": 30,
        },
        "networks": {network: None},
        "restart": "unless-stopped",
    }


def set_gc(service, gc):
    environment = service.setdefault("environment", {})
    for key in GC_KEYS:
        environment.pop(key, None)
    environment.update(GC_VARIANTS[gc])


def telemetry_of(config):
    """aspire: the compose runs its own aspire-dashboard; victoria: it exports into the stack of the host (the external
    network `observability`)."""
    has_dashboard = DASHBOARD in config["services"]
    has_stack = STACK_NETWORK in config.get("networks", {})
    if has_dashboard == has_stack:
        raise ValueError(f"the compose of the game must have either {DASHBOARD} or the network {STACK_NETWORK}")
    return "aspire" if has_dashboard else "victoria"


def add_stack(services, volumes, stack):
    for name, service in stack["services"].items():
        if name in services:
            raise ValueError(f"the stack service {name} is also a service of the game")
        services[name] = service

    for name in stack.get("volumes", {}):
        volumes[name] = {}


def transform(config, *, run, namespace, gc, cpus, postgres_image, env, stack=None):
    if gc not in GC_VARIANTS:
        raise ValueError(f"unknown GC variant {gc}: {', '.join(GC_VARIANTS)}")
    if (telemetry_of(config) == "victoria") != (stack is not None):
        raise ValueError(f"the stack config is given exactly when the game exports into it (network {STACK_NETWORK})")

    services = config["services"]
    networks = config["networks"]
    volumes = config.get("volumes", {})
    if POSTGRES in services:
        raise ValueError(f"the compose of the game already has a service {POSTGRES}")

    own = sorted(n for n in networks if n != STACK_NETWORK)
    if not own:
        raise ValueError("the compose of the game declares no network of its own")
    network = own[0]

    if stack is not None:
        add_stack(services, volumes, stack)

    for name, service in services.items():
        if "build" in service:
            service["build"]["network"] = "host"
            service["image"] = f"backend-measurements/{name}:local"
            set_gc(service, gc)
        if "DB_HOST" in service.get("environment", {}):
            service.setdefault("depends_on", {})[POSTGRES] = {"condition": "service_healthy", "required": True}
        service.pop("ports", None)
        service.pop("container_name", None)
        if "healthcheck" in service:
            service["healthcheck"]["start_period"] = START_PERIOD

    services[POSTGRES] = postgres_service(postgres_image, env, network)
    volumes["postgres-data"] = {}

    for name, service in services.items():
        service["container_name"] = f"{name}-{run}"
        service["cpus"] = cpus
        labels = service.get("labels") or {}
        if isinstance(labels, list):
            labels = dict(label.split("=", 1) for label in labels)
        labels["coolify.projectName"] = namespace
        service["labels"] = labels

    config["name"] = run
    config["networks"] = {key: {"name": f"{run}_{key}", "driver": "bridge"} for key in networks}
    config["volumes"] = {key: {"name": f"{run}_{key}"} for key in volumes}
    config["services"] = services
    return config


def read_env(path):
    env = {}
    for line in Path(path).read_text().splitlines():
        if line and not line.startswith("#"):
            key, value = line.split("=", 1)
            env[key] = value
    return env


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)

    env_command = commands.add_parser("env")
    env_command.add_argument("compose")

    commands.add_parser("telemetry")

    transform_command = commands.add_parser("transform")
    transform_command.add_argument("--run", required=True, help="compose project of the run")
    transform_command.add_argument("--namespace", required=True, help="the Coolify project name of the containers")
    transform_command.add_argument("--gc", required=True, choices=GC_VARIANTS)
    transform_command.add_argument("--cpus", required=True, type=float)
    transform_command.add_argument("--postgres-image", required=True)
    transform_command.add_argument("--env", required=True, help="the env file written by `env`")
    transform_command.add_argument("--stack", help="docker compose config --format json of deploy/observability")

    args = parser.parse_args(argv)

    if args.command == "env":
        values = env_values(required_variables(Path(args.compose).read_text()))
        return "\n".join(f"{key}={value}" for key, value in values.items())

    if args.command == "telemetry":
        return telemetry_of(json.load(sys.stdin))

    stack = json.loads(Path(args.stack).read_text()) if args.stack else None
    config = transform(json.load(sys.stdin), run=args.run, namespace=args.namespace, gc=args.gc, cpus=args.cpus, postgres_image=args.postgres_image,
                       env=read_env(args.env), stack=stack)
    return json.dumps(config, indent=2)


if __name__ == "__main__":
    print(main(sys.argv[1:]))
