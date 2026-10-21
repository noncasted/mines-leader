#!/usr/bin/env bash
# Run every test suite of the repo locally: backend, container generator, Unity EditMode.
#
# There is no CI: this script is the only full run. Unity tests need the local alpha editor.
# /deploy-client runs this script before it builds the WebGL client and stops on any failure.
#
# Everything runs in a snapshot of the working tree, made with `cp -a --reflink=always` on btrfs
# (the copy, Library included, costs almost nothing and takes seconds):
#   - the open shared editor keeps the project (Unity cannot open one project twice);
#   - the backend is built in the copy, so the bin folders the local Aspire cluster runs from
#     are not overwritten under it;
#   - every suite sees the same state of the tree.
# The copy is deleted on exit (KEEP_COPY=1 keeps it for a post-mortem).
#
# Steps, in this order (a failing step does not stop the next one; the summary lists all):
#   backend    dotnet build backend/backend.slnx + backend/Tools/Tests/bin/Debug/net10.0/Tests
#              (Postgres comes from Testcontainers - Docker must be running)
#   generator  dotnet build client/Tools~/ContainerGenerator -c Release + ContainerGenerator.Verify
#   unity      Unity -batchmode -nographics -runTests -testPlatform EditMode on the copied client
#              (all EditMode test assemblies of the project: Internal.Tests, ...)
#
# Environment (all optional):
#   TEST_COPY_DIR=/var/tmp          # parent of the snapshot; must be on the same btrfs as the repo
#                                   # (/tmp is tmpfs here - reflink is impossible there)
#   TEST_LOG_DIR=<dir>              # where logs and the Unity XML go; default /tmp/mines-leader-tests/<time>
#   UNITY_EDITOR=<path>             # Unity binary; default: version from ProjectVersion.txt under
#                                   # /tools/unity/<ver>/Editor/Unity or ~/Unity/Hub/Editor/<ver>/Editor/Unity
#   UNITY_MIN_FREE_GB=8             # refuse to start Unity when MemAvailable is below this
#   UNITY_TEST_FILTER=<filter>      # passed to Unity as -testFilter (e.g. a class full name)
#   UNITY_TIMEOUT=3600              # seconds before a hung Unity run is killed
#   KEEP_COPY=1                     # do not delete the snapshot
#
# Usage:
#   tools/scripts/run-tests.sh                   # all steps
#   tools/scripts/run-tests.sh --skip-unity      # backend + generator
#   tools/scripts/run-tests.sh --skip-backend    # generator + unity
#
# Exit code: 0 when every step that ran passed, 1 otherwise, 2 on bad arguments.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

SKIP_UNITY=0
SKIP_BACKEND=0

usage() {
    sed -n '2,/^set -euo/p' "${BASH_SOURCE[0]}" | sed '$d' | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-unity) SKIP_UNITY=1 ;;
        --skip-backend) SKIP_BACKEND=1 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done

BACKEND_LOCK=/tmp/claude-1000/mines-leader-backend-build.lock
UNITY_LOCK=/tmp/claude-1000/unity-batchmode.lock
mkdir -p /tmp/claude-1000

COPY_PARENT="${TEST_COPY_DIR:-/var/tmp}"
LOG_DIR="${TEST_LOG_DIR:-/tmp/mines-leader-tests/$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$LOG_DIR"

WORK="$(mktemp -d "$COPY_PARENT/mines-leader-tests.XXXXXX")"
COPY="$WORK/repo"

cleanup() {
    if [[ "${KEEP_COPY:-0}" == "1" ]]; then
        echo "Snapshot kept: $WORK"
    else
        rm -rf "$WORK"
    fi
}
trap cleanup EXIT

# Summary rows: "<step>|<PASS/FAIL/SKIP>|<seconds>|<details>"
SUMMARY=()
FAILED=0

record() {
    SUMMARY+=("$1|$2|$3|$4")
    if [[ "$2" == "FAIL" ]]; then
        FAILED=1
    fi
}

section() {
    echo
    echo "==== $* ===="
}

# --- Snapshot ------------------------------------------------------------------------------------

# Copies one path of the repo into the snapshot. Fails when reflink is impossible (another
# filesystem): a plain copy of client/Library would be gigabytes.
snap() {
    local rel="$1"
    mkdir -p "$COPY/$(dirname "$rel")"
    cp -a --reflink=always "$ROOT/$rel" "$COPY/$rel"
}

section "Snapshot -> $COPY"
SNAP_START=$SECONDS

mkdir -p "$COPY"
for f in .editorconfig .gitattributes; do
    snap "$f"
done
snap shared

# The backend build lock: no other session is half way through writing backend/**/obj while it
# is copied.
(
    flock 9
    snap backend
) 9>"$BACKEND_LOCK"
rm -rf "$COPY/backend/.telemetry"

# client: everything but caches and build outputs the tests do not need. Library stays (minus the
# player build caches) - without it Unity reimports the whole project. Temp holds the lock file of
# the open editor.
mkdir -p "$COPY/client"
shopt -s dotglob nullglob
for entry in "$ROOT"/client/*; do
    name="$(basename "$entry")"
    case "$name" in
        Temp|Logs|build|Build|Builds|MemoryCaptures|ProfilerCaptures|Recordings|traces|obj) continue ;;
    esac
    if [[ "$SKIP_UNITY" == "1" && "$name" != "Tools~" ]]; then
        continue
    fi
    if [[ "$name" == "Library" ]]; then
        continue
    fi
    snap "client/$name"
done
# Library minus the caches of player builds (WebGL builds, stripping backups): gigabytes in tens
# of thousands of files that EditMode tests never read.
if [[ "$SKIP_UNITY" != "1" ]]; then
    for entry in "$ROOT"/client/Library/*; do
        name="$(basename "$entry")"
        case "$name" in
            BuildCache|BuildHistory|BuildPlayerData|PlayerDataCache|com.unity.web.stripping-tool|ManagedCapture) continue ;;
        esac
        snap "client/Library/$name"
    done
fi
shopt -u dotglob nullglob

echo "Snapshot ready in $((SECONDS - SNAP_START)) s, logs in $LOG_DIR"

# --- Backend -------------------------------------------------------------------------------------

run_backend() {
    section "Backend"
    local start=$SECONDS
    local build_log="$LOG_DIR/backend-build.log"
    local test_log="$LOG_DIR/backend-tests.log"

    if ! docker info >/dev/null 2>&1; then
        echo "Docker is not running - backend tests need it (Testcontainers Postgres)." >&2
        record backend FAIL $((SECONDS - start)) "docker is not running"
        return
    fi

    echo "Building backend/backend.slnx (log: $build_log)"
    if ! (cd "$COPY" && dotnet build backend/backend.slnx) >"$build_log" 2>&1; then
        grep -E ": error " "$build_log" | sort -u | head -30 || true
        record backend FAIL $((SECONDS - start)) "build failed, see $build_log"
        return
    fi

    echo "Running backend tests (log: $test_log)"
    local code=0
    (cd "$COPY/backend/Tools/Tests/bin/Debug/net10.0" && ./Tests) >"$test_log" 2>&1 || code=$?

    # Microsoft Testing Platform summary, one value per line:
    #   Test run summary: Passed! - .../Tests.dll (net10.0|x64)
    #     total: 1305
    #     failed: 0
    #     succeeded: 1305
    #     skipped: 0
    #     duration: 2m 42s 825ms
    local totals
    totals="$(grep -A5 "^Test run summary" "$test_log" | tail -5 | sed 's/^ *//' | paste -sd ',' | sed 's/,/, /g')"
    if [[ $code -ne 0 ]]; then
        echo "Failed tests:"
        grep -E "^\s*failed " "$test_log" | sed 's/^ */  /' | head -50 || true
        local results="$COPY/backend/Tools/Tests/bin/Debug/net10.0/TestResults"
        if [[ -d "$results" ]]; then
            mkdir -p "$LOG_DIR/backend-TestResults"
            for f in "$results"/*.log; do
                [[ -e "$f" ]] || continue
                iconv -f UTF-16LE -t UTF-8 "$f" >"$LOG_DIR/backend-TestResults/$(basename "${f%.log}").utf8.log" || true
            done
        fi
        record backend FAIL $((SECONDS - start)) "exit $code; ${totals:-no summary}; see $test_log"
        return
    fi
    record backend PASS $((SECONDS - start)) "${totals:-no summary}"
}

# --- Container generator -------------------------------------------------------------------------

run_generator() {
    section "Container generator"
    local start=$SECONDS
    local log="$LOG_DIR/generator.log"
    local tools="$COPY/client/Tools~"

    echo "Building ContainerGenerator and running ContainerGenerator.Verify (log: $log)"
    if ! dotnet build "$tools/ContainerGenerator/ContainerGenerator.csproj" -c Release >"$log" 2>&1; then
        grep -E ": error " "$log" | sort -u | head -30 || true
        record generator FAIL $((SECONDS - start)) "build failed, see $log"
        return
    fi
    if ! dotnet run --project "$tools/ContainerGenerator.Verify/ContainerGenerator.Verify.csproj" -c Release >>"$log" 2>&1; then
        tail -30 "$log"
        record generator FAIL $((SECONDS - start)) "verify failed, see $log"
        return
    fi
    record generator PASS $((SECONDS - start)) "$(tail -1 "$log")"
}

# --- Unity EditMode ------------------------------------------------------------------------------

find_unity() {
    if [[ -n "${UNITY_EDITOR:-}" ]]; then
        echo "$UNITY_EDITOR"
        return
    fi
    local version
    version="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/client/ProjectSettings/ProjectVersion.txt")"
    local candidate
    for candidate in "/tools/unity/$version/Editor/Unity" "$HOME/Unity/Hub/Editor/$version/Editor/Unity"; do
        if [[ -x "$candidate" ]]; then
            echo "$candidate"
            return
        fi
    done
    echo "Unity $version not found (set UNITY_EDITOR)" >&2
    return 1
}

# Prints "<total> <passed> <failed> <skipped> <result>" and the failed tests of an NUnit 3 XML.
parse_unity_results() {
    python3 -I - "$1" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
a = root.attrib
print(a.get("total", "?"), a.get("passed", "?"), a.get("failed", "?"), a.get("skipped", "?"), a.get("result", "?"))
for case in root.iter("test-case"):
    if case.get("result") != "Failed":
        continue
    message = case.findtext("failure/message") or ""
    first = next((line.strip() for line in message.splitlines() if line.strip()), "")
    print(f"  {case.get('fullname')}: {first[:200]}")
PY
}

run_unity() {
    section "Unity EditMode"
    local start=$SECONDS
    local log="$LOG_DIR/unity.log"
    local xml="$LOG_DIR/unity-results.xml"

    local unity
    if ! unity="$(find_unity)"; then
        record unity FAIL $((SECONDS - start)) "editor not found"
        return
    fi

    local avail_gb
    avail_gb=$(( $(sed -n 's/^MemAvailable: *\([0-9]*\) kB/\1/p' /proc/meminfo) / 1024 / 1024 ))
    if (( avail_gb < ${UNITY_MIN_FREE_GB:-8} )); then
        echo "Only $avail_gb GB of memory available, Unity batchmode needs ${UNITY_MIN_FREE_GB:-8}." >&2
        record unity FAIL $((SECONDS - start)) "low memory: $avail_gb GB available"
        return
    fi

    local args=(-batchmode -nographics -projectPath "$COPY/client" -runTests -testPlatform EditMode
        -testResults "$xml" -logFile "$log")
    if [[ -n "${UNITY_TEST_FILTER:-}" ]]; then
        args+=(-testFilter "$UNITY_TEST_FILTER")
    fi

    echo "Unity: $unity"
    echo "Running EditMode tests (log: $log; waits for other batchmode runs of this script)"
    local code=0
    (
        flock 9
        timeout --kill-after=60 "${UNITY_TIMEOUT:-3600}" "$unity" "${args[@]}" >/dev/null 2>&1
    ) 9>"$UNITY_LOCK" || code=$?

    if [[ ! -s "$xml" ]]; then
        echo "No test results (Unity exit code $code). Last log lines:" >&2
        grep -n -E "error CS|Caught fatal signal|Aborting|Crash|Exception" "$log" | tail -20 >&2 || true
        record unity FAIL $((SECONDS - start)) "no results, exit $code; see $log"
        return
    fi

    local parsed totals
    parsed="$(parse_unity_results "$xml")"
    totals="$(head -1 <<<"$parsed")"
    local total passed failed skipped result
    read -r total passed failed skipped result <<<"$totals"
    local details="total $total, passed $passed, failed $failed, skipped $skipped"

    if [[ $code -ne 0 || "$failed" != "0" ]]; then
        echo "Failed tests:"
        tail -n +2 <<<"$parsed"
        record unity FAIL $((SECONDS - start)) "exit $code; $details; see $xml"
        return
    fi
    record unity PASS $((SECONDS - start)) "$details"
}

# --- Run -----------------------------------------------------------------------------------------

if [[ "$SKIP_BACKEND" == "1" ]]; then
    record backend SKIP 0 "--skip-backend"
else
    run_backend
fi

run_generator

if [[ "$SKIP_UNITY" == "1" ]]; then
    record unity SKIP 0 "--skip-unity"
else
    run_unity
fi

section "Summary"
for row in "${SUMMARY[@]}"; do
    IFS='|' read -r step status seconds details <<<"$row"
    printf '%-10s %-5s %5ss  %s\n' "$step" "$status" "$seconds" "$details"
done
echo "Logs: $LOG_DIR"

if [[ "$FAILED" == "1" ]]; then
    echo "TESTS FAILED"
    exit 1
fi
echo "ALL TESTS PASSED"
