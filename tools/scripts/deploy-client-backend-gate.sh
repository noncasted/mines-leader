#!/usr/bin/env bash
# The "beforeUpload" of .claude/deploy-client.json: the live meta backend accepts the protocol hash of the tree.
#
# A client ahead of the backend gets the "version outdated" notice on every connection (2026-11-03), so the upload
# waits for the backend: a websocket upgrade to META_URL/?protocol=<hash> that is not closed with 4001 within
# PROBE_SECONDS is accepted; a close with 4001 is a backend on another protocol; anything else (no 101: a backend
# restarting) is not ready. Probes every PROBE_INTERVAL seconds until BACKEND_WAIT seconds, then exit 1.
#
# Environment: META_URL (https://meta.minesleader.xyz), BACKEND_WAIT (900), PROBE_INTERVAL (15), PROBE_SECONDS (4).
# Run from the root of the game. Exit 0 = upload; the last line is the detail of the deploy-client summary.
set -euo pipefail

META_URL="${META_URL:-https://meta.minesleader.xyz}"
BACKEND_WAIT="${BACKEND_WAIT:-900}"
PROBE_INTERVAL="${PROBE_INTERVAL:-15}"
PROBE_SECONDS="${PROBE_SECONDS:-4}"

hash="$(sed -n 's/.*Value = "\([0-9a-f]*\)".*/\1/p' shared/Protocol/ProtocolHash.cs)"
[[ -n "$hash" ]] || { echo "no ProtocolHash.Value in shared/Protocol/ProtocolHash.cs"; exit 1; }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# Prints accepted / outdated / "not ready: <why>".
probe() {
    rm -f "$work/headers" "$work/body"
    curl -s -N --http1.1 --max-time "$PROBE_SECONDS" -D "$work/headers" -o "$work/body" \
        -H "Connection: Upgrade" -H "Upgrade: websocket" -H "Sec-WebSocket-Version: 13" \
        -H "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==" "$META_URL/?protocol=$hash" || true
    local status body
    status="$(head -1 "$work/headers" 2>/dev/null | tr -d '\r')"
    if [[ "$status" != *" 101"* ]]; then
        echo "not ready: ${status:-no answer}"
        return
    fi
    body="$(od -An -tx1 "$work/body" 2>/dev/null | tr -d ' \n')"
    case "$body" in
        88??0fa1*) echo outdated ;;
        88*) echo "not ready: closed with frame ${body:0:12}" ;;
        *) echo accepted ;;
    esac
}

start=$SECONDS
last=""
while true; do
    state="$(probe)"
    if [[ "$state" == accepted ]]; then
        echo "backend $META_URL accepts protocol $hash"
        exit 0
    fi
    [[ "$state" == "$last" ]] || echo "$(date +%T) $META_URL, protocol $hash: $state"
    last="$state"
    if ((SECONDS - start >= BACKEND_WAIT)); then
        echo "backend $META_URL does not accept protocol $hash after $BACKEND_WAIT s ($state): nothing uploaded"
        exit 1
    fi
    sleep "$PROBE_INTERVAL"
done
