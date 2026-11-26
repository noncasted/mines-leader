# Puts the tools on the path and names the committed snapshots the tests read:
#   python3 -m unittest discover -s backend/measurements/tools/tests
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent.parent
RESULTS = TOOLS.parent / "results"
PROD_BEFORE = RESULTS / "2026-11-25-prod-before"
MONOLITH = RESULTS / "2026-11-25-monolith"

sys.path.insert(0, str(TOOLS))
