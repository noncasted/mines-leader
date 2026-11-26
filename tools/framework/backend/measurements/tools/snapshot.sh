#!/usr/bin/env bash
# Read-only memory snapshot of Coolify hosts or of this machine: free, docker stats, the labels of every container,
# smaps of every process and the cgroup memory (with swap) of every container. Writes <out>/<name>.txt and
# <out>/<name>.cgroup.txt; tools/prod_report.py turns the folder into tables.
#
#   backend/measurements/tools/snapshot.sh results/<date>-<label> firstvds ovh
#   SNAPSHOT_FILTER=label=com.docker.compose.project=<run> backend/measurements/tools/snapshot.sh <out> local:<name>
#
# A host is an ssh alias (~/.ssh/config); a non-root user needs passwordless sudo for /proc and the cgroups.
# `local:<name>` snapshots this machine into <name>.txt (local.sh, one file per variant): the smaps of the container
# processes are read by a throwaway alpine container in the host PID namespace, so no sudo is needed.
# SNAPSHOT_FILTER (a `docker ps --filter`) limits the containers; nothing matching is an error.
set -euo pipefail

out=${1:?usage: snapshot.sh <out-folder> <ssh-host | local:<name>>...}
shift
[ $# -gt 0 ] || { echo "no hosts given" >&2; exit 2; }
mkdir -p "$out"
filter=${SNAPSHOT_FILTER:-}

# $1: the filter, $2: remote | local. D runs docker, R runs a command that reads /proc of any process.
PREAMBLE='
set -euo pipefail
filter=$1
if [ "$2" = local ]; then
    D=docker
    R="docker run --rm -i --pid=host --cap-add SYS_PTRACE --network none alpine:latest"
    S=
else
    sudo -n true 2>/dev/null && S=sudo || S=
    D="$S docker"
    R=$S
fi
ids=$($D ps -q --no-trunc ${filter:+--filter "$filter"})
[ -n "$ids" ] || { echo "no running containers${filter:+ match $filter}" >&2; exit 1; }
'

MEMORY='
echo "#host $(hostname) $(date -u +%FT%TZ) nproc=$(nproc) uptime=$(cut -d" " -f1 /proc/uptime)"
free -m
echo "#df"
df -h / | tail -1
echo "#stats"
$D stats --no-stream --format "{{.Name}}\t{{.MemUsage}}\t{{.CPUPerc}}" $ids
echo "#labels"
for c in $ids; do
    $D inspect -f "{{.Name}}\t{{index .Config.Labels \"com.docker.compose.project\"}}\t{{index .Config.Labels \"coolify.projectName\"}}\t{{index .Config.Labels \"coolify.serviceName\"}}\t{{index .Config.Labels \"coolify.resourceName\"}}\t{{.Config.Image}}\t{{.State.StartedAt}}\t{{.State.Pid}}" "$c"
done
echo "#pss"
for c in $ids; do
    n=$($D inspect -f "{{.Name}}" "$c")
    for pid in $($D top "$c" -eo pid | tail -n +2); do
        echo "$n $pid"
    done
done | $R sh -c '\''while read -r n pid; do echo "$n $pid $(awk "/^Rss:/{r=\$2}/^Pss:/{p=\$2}/^Anonymous:/{a=\$2}END{print r,p,a}" /proc/$pid/smaps_rollup 2>/dev/null)"; done'\''
'

CGROUP='
for c in $ids; do
    n=$($D inspect -f "{{.Name}}" "$c")
    d=/sys/fs/cgroup/system.slice/docker-$c.scope
    [ -d "$d" ] || d=$(find /sys/fs/cgroup -maxdepth 4 -type d -name "*$c*" 2>/dev/null | head -1)
    stat() { $S awk -v k="$1" "\$1 == k {print \$2}" "$d/memory.stat"; }
    echo -e "$n\t$($S cat "$d/memory.current")\t$($S cat "$d/memory.swap.current")\t$(stat anon)\t$(stat file)\t$(stat inactive_file)"
done
'

run() {
    local host=$1 body=$2
    if [[ $host == local:* ]]; then
        bash -s -- "$filter" local <<<"$body"
    else
        ssh -o BatchMode=yes "$host" "bash -s -- $(printf %q "$filter") remote" <<<"$body"
    fi
}

for host in "$@"; do
    name=${host#local:}
    [ -n "$name" ] || { echo "local: needs a name, local:<name>" >&2; exit 2; }
    run "$host" "$PREAMBLE$MEMORY" > "$out/$name.txt"
    run "$host" "$PREAMBLE$CGROUP" > "$out/$name.cgroup.txt"
    echo "$name: $(wc -l < "$out/$name.txt") + $(wc -l < "$out/$name.cgroup.txt") lines"
done
