#!/usr/bin/env bash
# Local memory run of a game backend: for every variant it builds the deploy compose of the game in Release, starts it
# with its own Postgres, waits until every service is healthy, leaves it idle, snapshots its containers (snapshot.sh,
# the metrics of the prod snapshots) and removes its containers and volumes. Prints the summary (prod_report.py).
#
#   backend/measurements/tools/local.sh <out-folder> <project> [<layout>/<gc>...]
#
#   project    a key of deploy/registry.json: the deploy branch of its repository (or LOCAL_REF) is checked out into
#              .build/measurements/<project> (git-ignored), never a working tree of the game
#   layout     services | monolith: the compose file of the layout is `compose` of the project in the registry
#   gc         server (the default of the Web SDK, DATAS) | workstation (DOTNET_gcServer=0) |
#              conserve (DOTNET_gcServer=0, DOTNET_GCConserveMemory=5)
#   no variant: the full set, see VARIANTS below
#
# The telemetry comes with the source: `aspire` when its compose runs aspire-dashboard, `victoria` when it exports into
# the stack of the host (network `observability`), which the run then starts from deploy/observability of this tree.
#
# Writes <out>/<layout>-<gc>-<telemetry>.txt and .cgroup.txt per variant and <out>/conditions.info (source commit,
# idle time, cpus); the build and compose logs go to .build/measurements/runs/<out name>/.
# Environment: LOCAL_REF - a full commit hash or a branch of the repository instead of the deploy branch; LOCAL_IDLE - seconds between healthy and the snapshot (default 300); LOCAL_CPUS - processors of every
# container (default 6, the cores of ovh); LOCAL_POSTGRES - the Postgres image (default postgres:18-alpine, as prod).
# Needs docker with compose and network access for the clone and the build (the build runs on the host network).
set -euo pipefail

VARIANTS=(services/server services/workstation services/conserve monolith/server monolith/workstation monolith/conserve)

out=${1:?usage: local.sh <out-folder> <project> [<layout>/<gc>...]}
project=${2:?usage: local.sh <out-folder> <project> [<layout>/<gc>...]}
shift 2
(( $# > 0 )) && VARIANTS=("$@")

TOOLS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$TOOLS/../../.." && pwd)"
IDLE=${LOCAL_IDLE:-300}
CPUS=${LOCAL_CPUS:-6}
POSTGRES=${LOCAL_POSTGRES:-postgres:18-alpine}
CHECKOUT="$ROOT/.build/measurements/$project"
mkdir -p "$out"
out="$(cd "$out" && pwd)"
WORK="$ROOT/.build/measurements/runs/$(basename "$out")"
mkdir -p "$WORK"

for variant in "${VARIANTS[@]}"; do
    IFS=/ read -r layout gc <<<"$variant"
    case "$layout" in services|monolith) ;; *) echo "unknown layout in $variant" >&2; exit 2 ;; esac
    case "$gc" in server|workstation|conserve) ;; *) echo "unknown gc in $variant" >&2; exit 2 ;; esac
done

# The registry of this tree (registry.py of tools/scripts/deploy): repository, branch, the Coolify project name (the label
# coolify.projectName of the hosts) and the compose file of each layout.
read -r repository branch coolify_name services_compose monolith_compose < <(python3 - "$ROOT" "$project" <<'EOF'
import sys
sys.path.insert(0, f"{sys.argv[1]}/tools/scripts/deploy")
import registry
entry = registry.find(registry.load(f"{sys.argv[1]}/deploy/registry.json"), sys.argv[2])
compose = entry.get("compose") or sys.exit(f"{sys.argv[2]}: no compose in the registry (deploy/CLAUDE.md)")
print(entry["repository"], entry["branch"], entry["coolify"]["name"], compose.get("services", "-"), compose.get("monolith", "-"))
EOF
)
[[ -n "${monolith_compose:-}" ]] || { echo "cannot read $project from deploy/registry.json" >&2; exit 2; }

ref=${LOCAL_REF:-$branch}
[[ -d "$CHECKOUT/.git" ]] || git clone -q --depth 1 --branch "$branch" "https://github.com/$repository.git" "$CHECKOUT"
git -C "$CHECKOUT" fetch -q --depth 1 origin "$ref"
git -C "$CHECKOUT" reset -q --hard FETCH_HEAD
git -C "$CHECKOUT" clean -qfdx
commit=$(git -C "$CHECKOUT" rev-parse --short HEAD)
cat > "$out/conditions.info" <<EOF
source $repository $ref $commit
idle ${IDLE}s after healthy, cpus $CPUS per container, $POSTGRES
machine $(hostname) nproc=$(nproc) load="$(cut -d' ' -f1-3 /proc/loadavg)" $(free -m | awk '/^Mem:/{print "ram=" $2 "MiB"}') docker $(docker version -f '{{.Server.Version}}')
EOF
echo "$project $ref @ $commit -> $out"

run=
compose() { docker compose -p "$run" -f "$WORK/$run.json" "$@"; }
cleanup() {
    if [[ -n "$run" && -f "$WORK/$run.json" ]]; then
        compose down -v --remove-orphans >>"$WORK/$run.log" 2>&1
        run=
    fi
}
trap cleanup EXIT

for variant in "${VARIANTS[@]}"; do
    IFS=/ read -r layout gc <<<"$variant"
    relative=$services_compose
    [[ "$layout" == monolith ]] && relative=$monolith_compose
    [[ "$relative" != - ]] || { echo "$project: no compose for the layout $layout in the registry" >&2; exit 2; }
    file="$CHECKOUT/$relative"
    # `run` names the compose project for the cleanup: it is set only once the compose file of the run exists.
    id="measure-$(basename "$out")-$layout-$gc"
    env="$WORK/$id.env"

    (umask 077 && python3 "$TOOLS/local_compose.py" env "$file" > "$env")
    docker compose -p "$id" --env-file "$env" -f "$file" config --format json > "$WORK/$id.game.json" 2>>"$WORK/$id.log"
    telemetry=$(python3 "$TOOLS/local_compose.py" telemetry < "$WORK/$id.game.json")
    name="$layout-$gc-$telemetry"
    stack=()
    if [[ "$telemetry" == victoria ]]; then
        docker compose -p "$id" -f "$ROOT/deploy/observability/docker-compose.yaml" config --format json \
            > "$WORK/$id.stack.json" 2>>"$WORK/$id.log"
        stack=(--stack "$WORK/$id.stack.json")
    fi
    (umask 077 && python3 "$TOOLS/local_compose.py" transform --run "$id" --namespace "$coolify_name" --gc "$gc" \
        --cpus "$CPUS" --postgres-image "$POSTGRES" --env "$env" "${stack[@]}" \
        < "$WORK/$id.game.json" > "$WORK/$id.json")

    run=$id

    # A run killed before its cleanup leaves its containers and volumes under the same name: start from nothing.
    compose down -v --remove-orphans >>"$WORK/$run.log" 2>&1
    echo "$name: build"
    compose build >>"$WORK/$run.log" 2>&1 || { tail -30 "$WORK/$run.log" >&2; exit 1; }
    echo "$name: up"
    if ! compose up -d --wait --wait-timeout 600 >>"$WORK/$run.log" 2>&1; then
        compose logs --no-color --tail 300 >>"$WORK/$run.log" 2>&1
        tail -60 "$WORK/$run.log" >&2
        echo "$name: not healthy, the log: $WORK/$run.log" >&2
        exit 1
    fi
    echo "$name: healthy at $(date -u +%T), idle ${IDLE}s"
    sleep "$IDLE"
    SNAPSHOT_FILTER="label=com.docker.compose.project=$run" "$TOOLS/snapshot.sh" "$out" "local:$name"
    cleanup
done

python3 "$TOOLS/prod_report.py" --summary "$out"
