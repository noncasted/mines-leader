#!/usr/bin/env bash
# Run every test suite of the repo locally: backend, container generator, Unity EditMode.
#
# This script is the only full run. The CI of GitHub (.github/workflows/ci.yml) builds and tests only the backend; the
# container generator and the Unity EditMode tests run here alone (Unity tests stay out of CI by the decision of the user).
# /deploy-client runs this script before it builds the WebGL client and stops on any failure: the EditMode tests are the
# gate of every deploy.
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
#   generator  dotnet build tools/framework/client/core/Source~/ContainerGenerator -c Release + ContainerGenerator.Verify
#   unity      Unity -batchmode -nographics -runTests -testPlatform EditMode on the copied client
#              (all EditMode test assemblies of the project: GamePlay.Tests, GamePlay.Metrics.Tests, Flow.Tests,
#              Global.Tests, Internal.Content.Tests and the tests of the packages in "testables" of the manifest: core,
#              match-metrics, network); a watchdog kills the run when a script compilation is
#              requested after the start of the tests (the UniTask waits would stop until the limit)
#
# Environment (all optional):
#   TEST_COPY_DIR=/var/tmp          # parent of the snapshot; must be on the same btrfs as the repo
#                                   # (/tmp is tmpfs here - reflink is impossible there)
#   TEST_LOG_DIR=<dir>              # where logs, the Unity XML and report.md go; default /tmp/mines-leader-tests/<time>
#   UNITY_EDITOR=<path>             # Unity binary; default: version from ProjectVersion.txt under
#                                   # /tools/unity/<ver>/Editor/Unity or ~/Unity/Hub/Editor/<ver>/Editor/Unity
#   UNITY_MIN_FREE_GB=8             # refuse to start Unity when MemAvailable is below this
#   UNITY_TEST_FILTER=<filter>      # passed to Unity as -testFilter (e.g. a class full name)
#   UNITY_TIMEOUT=3600              # seconds before a hung Unity run is killed (SIGKILL)
#   UNITY_LOCK=<file>               # lock shared by batchmode Unity runs; default /tmp/claude-1000/unity-batchmode.lock
#   KEEP_COPY=1                     # do not delete the snapshot
#
# Usage:
#   tools/scripts/run-tests.sh                   # all steps
#   tools/scripts/run-tests.sh --skip-unity      # backend + generator
#   tools/scripts/run-tests.sh --skip-backend    # generator + unity
#
# Report: <TEST_LOG_DIR>/report.md - the steps with their result and time, the Unity tests per assembly (total, passed,
# failed, skipped, seconds) and the failed tests; written on every run that reaches the summary.
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
UNITY_LOCK="${UNITY_LOCK:-/tmp/claude-1000/unity-batchmode.lock}"
mkdir -p /tmp/claude-1000

COPY_PARENT="${TEST_COPY_DIR:-/var/tmp}"
LOG_DIR="${TEST_LOG_DIR:-/tmp/mines-leader-tests/$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$LOG_DIR"

WORK="$(mktemp -d "$COPY_PARENT/mines-leader-tests.XXXXXX")"
COPY="$WORK/repo"

# Every process started by the Unity run carries this value in its environment (not exported: the script and its other
# steps do not carry it). Unity calls setsid and so do its import workers, the process group of `timeout` does not reach
# them.
RUN_MARKER="MINES_LEADER_TEST_RUN=$WORK"

# Kills what is left of the Unity run of this script. Unity.Licensing.Client stays: it outlives the editor that started
# it and serves every editor of the user. Fails when something survives SIGKILL.
kill_own_processes() {
    local attempt pid found
    for attempt in 1 2 3 4 5 6 7 8 9 10; do
        found=()
        for pid in /proc/[0-9]*; do
            pid="${pid#/proc/}"
            if grep -qzxF -e "$RUN_MARKER" "/proc/$pid/environ" 2>/dev/null \
                && [[ "$(cat "/proc/$pid/comm" 2>/dev/null)" != Unity.Licensing* ]]; then
                found+=("$pid")
            fi
        done
        if [[ ${#found[@]} -eq 0 ]]; then
            return 0
        fi
        kill -KILL "${found[@]}" 2>/dev/null || true
        sleep 1
    done
    echo "Processes of this run survived SIGKILL: ${found[*]}" >&2
    return 1
}

cleanup() {
    kill_own_processes || true
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
# The framework submodule: backend projects and Unity packages reference it.
snap tools/framework

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
    if [[ "$SKIP_UNITY" == "1" ]]; then
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
    local tools="$COPY/tools/framework/client/core/Source~"

    # SkipAnalyzersCopy: a Release build copies ContainerGenerator.dll into client/core/Analyzers of the snapshot, and
    # Unity would recompile the scripts of the tested tree for a dll the tests did not get (releases/0.8.0.md #16).
    echo "Building ContainerGenerator and running ContainerGenerator.Verify (log: $log)"
    if ! dotnet build "$tools/ContainerGenerator/ContainerGenerator.csproj" -c Release -p:SkipAnalyzersCopy=true >"$log" 2>&1; then
        grep -E ": error " "$log" | sort -u | head -30 || true
        record generator FAIL $((SECONDS - start)) "build failed, see $log"
        return
    fi
    if ! dotnet run --project "$tools/ContainerGenerator.Verify/ContainerGenerator.Verify.csproj" -c Release --property:SkipAnalyzersCopy=true >>"$log" 2>&1; then
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

# Markdown rows "| <assembly> | total | passed | failed | skipped | seconds |" of an NUnit 3 XML, then the failed tests as
# a list.
unity_assembly_rows() {
    python3 -I - "$1" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
for suite in root.iter("test-suite"):
    if suite.get("type") != "Assembly":
        continue
    a = suite.attrib
    name = a.get("name", "?").removesuffix(".dll")
    seconds = float(a.get("duration", "0"))
    print(f"| {name} | {a.get('total', '?')} | {a.get('passed', '?')} | {a.get('failed', '?')} | {a.get('skipped', '?')} | {seconds:.1f} |")
failed = [case for case in root.iter("test-case") if case.get("result") == "Failed"]
if failed:
    print()
    print("Failed tests:")
    print()
    for case in failed:
        message = case.findtext("failure/message") or ""
        first = next((line.strip() for line in message.splitlines() if line.strip()), "")
        print(f"- `{case.get('fullname')}`: {first[:200]}")
PY
}

# Watchdog of the Unity run: "<log>" "<found>" "<pid>". A script compilation requested after the start of the tests cannot
# run before their end (the test runner locks the reload), and while it is pending EditorApplication.isCompiling stays
# true: the UniTask loop of the editor stops, every test that awaits a frame or a timeout waits for the 180 s timeout of
# the test framework and the run reaches its limit with no XML. Kills the Unity of this run at once and writes the line
# of the log to <found>.
# A content build of a test (the Scriptable Build Pipeline, "Build Player Scripts") requests two compilations that write
# nothing into the tree: "Compiling scripts for player" (the player assemblies, apart from the editor ones) is never a
# change; "Recompiling all scripts before compiling player scripts because Auto Refresh is disabled." is none only when the
# next Tundra build reports "0 items updated", otherwise it is a real recompilation and kills the run.
watch_compile_during_run() {
    local log="$1" found="$2" pid="$3" line
    while kill -0 "$pid" 2>/dev/null; do
        if [[ -f "$log" ]]; then
            line="$(awk '/^Running tests for ExecutionSettings/ { tests = 1 }
                         !tests { next }
                         pending != "" && /^\*\*\* Tundra build / {
                             if ($0 !~ / 0 items updated,/) { print pending; exit }
                             pending = ""
                         }
                         /Requested script compilation because: Compiling scripts for player$/ { next }
                         /Requested script compilation because: Recompiling all scripts before compiling player scripts because Auto Refresh is disabled\.$/ {
                             pending = NR ": " $0
                             next
                         }
                         /Requested script compilation/ { print NR ": " $0; exit }' "$log")"
            if [[ -n "$line" ]]; then
                echo "$line" >"$found"
                kill_own_processes >/dev/null 2>&1 || true
                return 0
            fi
        fi
        sleep 5
    done
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

    local limit="${UNITY_TIMEOUT:-3600}"
    echo "Unity: $unity"
    echo "Running EditMode tests (limit: $limit s, log: $log; waits for other batchmode runs of this script)"
    exec 9>"$UNITY_LOCK"
    flock 9
    # SIGKILL at the limit: on SIGTERM Unity shuts down for tens of seconds, writes no XML and crashes. In the
    # background: `wait` lets the traps run. 9>&-: no process of Unity holds the lock.
    local code=0 run_start=$SECONDS compiled="$log.compile-during-run"
    rm -f "$compiled"
    (env "$RUN_MARKER" timeout --signal=KILL "$limit" "$unity" "${args[@]}"; exit $?) >/dev/null 2>&1 9>&- &
    local unity_pid=$!
    watch_compile_during_run "$log" "$compiled" "$unity_pid" 9>&- &
    local watcher=$!
    wait "$unity_pid" || code=$?
    wait "$watcher" 2>/dev/null || true
    local elapsed=$((SECONDS - run_start)) survived=""
    if ! kill_own_processes; then
        survived="; processes survived SIGKILL"
    fi
    exec 9>&-

    local reason=""
    if [[ -s "$compiled" ]]; then
        reason="killed: scripts changed during the test run, the UniTask waits would stop (log line $(cut -d: -f1 "$compiled"):"
        reason+=" Requested script compilation; what wrote a script, an asmdef or a dll into the tested tree?)"
    elif [[ $code -eq 137 && $elapsed -ge $limit ]]; then
        reason="limit $limit s"
    elif [[ $code -ne 0 && -f "$log" ]] && grep -q "Caught fatal signal" "$log"; then
        reason="crash: $(grep -m1 -o "Caught fatal signal - signo:[0-9]*" "$log"), exit $code"
    elif [[ $code -gt 128 ]]; then
        reason="killed by signal $((code - 128))"
    fi

    if [[ -n "$reason" || ! -s "$xml" ]]; then
        reason="${reason:-no results, exit $code}"
        echo "Unity: $reason. Last log lines:" >&2
        if [[ -f "$log" ]]; then
            grep -n -E "error CS|Caught fatal signal|Aborting|Crash|Exception" "$log" | grep -v -E ": warning " | tail -20 | cut -c1-200 >&2 || true
        fi
        record unity FAIL $((SECONDS - start)) "$reason$survived; see $log"
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

# The short report next to the logs; a run of /deploy-client leaves it in <its logs>/tests/report.md.
REPORT="$LOG_DIR/report.md"
{
    echo "# Tests $(date '+%Y-%m-%d %H:%M:%S')"
    echo
    echo "Result: $([[ "$FAILED" == "1" ]] && echo FAILED || echo PASSED), $SECONDS s"
    echo
    echo "| Step | Result | Seconds | Details |"
    echo "|------|--------|---------|---------|"
    for row in "${SUMMARY[@]}"; do
        IFS='|' read -r step status seconds details <<<"$row"
        echo "| $step | $status | $seconds | $details |"
    done
    if [[ -s "$LOG_DIR/unity-results.xml" ]]; then
        echo
        echo "Unity EditMode by assembly:"
        echo
        echo "| Assembly | Total | Passed | Failed | Skipped | Seconds |"
        echo "|----------|-------|--------|--------|---------|---------|"
        unity_assembly_rows "$LOG_DIR/unity-results.xml" || echo "(the XML could not be parsed)"
    fi
} >"$REPORT"

echo "Logs: $LOG_DIR"
echo "Report: $REPORT"

if [[ "$FAILED" == "1" ]]; then
    echo "TESTS FAILED"
    exit 1
fi
echo "ALL TESTS PASSED"
