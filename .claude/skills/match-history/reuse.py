# The parts of the client-telemetry skill this skill takes as they are: the url of the meta and the env file of the token (its
# section "## client-telemetry" of .claude/project-skills.md: the same meta, the same CONSOLE_TOKEN), the GET client that refuses
# redirects and keeps the token in the header, the exit codes, the scrubber of the output and the text tables.
# client-telemetry is a link into tools/framework, so its code is imported here, not moved to a shared place: a move would change
# the framework. Its module names (config, api, common, commands, report, ...) are taken: the modules of this skill are history_*.
import importlib.machinery
import importlib.util
import sys
from pathlib import Path

TELEMETRY = Path(__file__).resolve().parent.parent / "client-telemetry"

if not (TELEMETRY / "api.py").is_file():
    raise ImportError(f"match-history reuses the client-telemetry skill, not found at {TELEMETRY}")

sys.path.append(str(TELEMETRY))

import api  # noqa: E402
import common  # noqa: E402
import config  # noqa: E402
import text  # noqa: E402


def _load_cli():
    loader = importlib.machinery.SourceFileLoader("client_telemetry_cli", str(TELEMETRY / "telemetry"))
    spec = importlib.util.spec_from_loader(loader.name, loader)
    module = importlib.util.module_from_spec(spec)
    loader.exec_module(module)

    return module


Scrubbed = _load_cli()._Scrubbed
TelemetryApi = api.Api
Failure = common.Failure
USAGE = common.USAGE
SERVICE = common.SERVICE
AUTH = common.AUTH
NOT_FOUND = common.NOT_FOUND
resolve_settings = config.resolve
read_token = config.read_token
table = text.table
