#!/usr/bin/env bash
# Deploy the Unity WebGL client to the Coolify host.
#
# Uploads client/build/build into <DEPLOY_PATH>/releases/<tag> over SSH and flips
# the `current` symlink that the nginx container (client/deploy/docker-compose.yaml)
# serves. The container is never redeployed — the swap is atomic and instant.
#
# Credentials live outside the repo, in ~/.config/mines-leader/deploy.env:
#
#   DEPLOY_HOST=root@your-coolify-host      # ssh target
#   DEPLOY_PATH=/data/mines-leader-web      # must match the compose bind mount
#   DEPLOY_SSH_KEY=~/.ssh/id_ed25519        # optional, for key auth
#
# Password auth: put the password (and nothing else) in
# tools/scripts/.deploy-password — gitignored, read via `sshpass -f` so it never
# shows up in the process list. Requires the `sshpass` package.
#   UNITY=/path/to/Unity                    # optional, for --build
#   KEEP_RELEASES=5                         # optional, default 5
#
# Usage:
#   tools/scripts/deploy-webgl.sh                  # upload the existing build
#   tools/scripts/deploy-webgl.sh --build          # run Unity first, then upload
#   tools/scripts/deploy-webgl.sh --list           # show releases on the server
#   tools/scripts/deploy-webgl.sh --rollback <tag> # point `current` at an old release
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BUILD_DIR="$ROOT/client/build/build"
ENV_FILE="${DEPLOY_ENV_FILE:-$HOME/.config/mines-leader/deploy.env}"

if [[ -f "$ENV_FILE" ]]; then
    set -a
    source "$ENV_FILE"
    set +a
fi

: "${DEPLOY_HOST:?set DEPLOY_HOST in $ENV_FILE}"
DEPLOY_PATH="${DEPLOY_PATH:-/data/mines-leader-web}"
KEEP_RELEASES="${KEEP_RELEASES:-5}"

SSH_OPTS=()
if [[ -n "${DEPLOY_SSH_KEY:-}" ]]; then
    SSH_OPTS=(-i "${DEPLOY_SSH_KEY/#\~/$HOME}")
fi

# Password auth, if a password file is present: `-f` keeps the secret out of the
# process list, unlike `sshpass -p`.
PASS_FILE="${DEPLOY_PASSWORD_FILE:-$ROOT/tools/scripts/.deploy-password}"
SSH_CMD=(ssh)
if [[ -f "$PASS_FILE" ]]; then
    if ! command -v sshpass >/dev/null; then
        echo "$PASS_FILE exists but sshpass is not installed (sudo pacman -S sshpass)." >&2
        exit 1
    fi
    SSH_CMD=(sshpass -f "$PASS_FILE" ssh)
    # Every ssh/rsync invocation authenticates separately; without this a typo in
    # the password file turns into five prompts instead of one clear failure.
    SSH_OPTS+=(-o PreferredAuthentications=password -o PubkeyAuthentication=no -o BatchMode=no)
fi

remote() { "${SSH_CMD[@]}" "${SSH_OPTS[@]}" "$DEPLOY_HOST" "$@"; }

run_build=0
case "${1:-}" in
    --build)
        run_build=1
        ;;
    --list)
        remote "ls -1 '$DEPLOY_PATH/releases' && echo '--- current:' && readlink '$DEPLOY_PATH/current'"
        exit 0
        ;;
    --rollback)
        target="${2:?usage: --rollback <tag>}"
        remote "test -d '$DEPLOY_PATH/releases/$target'"
        remote "cd '$DEPLOY_PATH' && ln -sfn 'releases/$target' current.tmp && mv -Tf current.tmp current"
        echo "Rolled back to $target"
        exit 0
        ;;
    "") ;;
    *)
        echo "Unknown option: $1" >&2
        exit 2
        ;;
esac

if [[ $run_build == 1 ]]; then
    UNITY="${UNITY:-${UNITY_PATH:-unity}}"
    echo "==> Unity WebGL build ($UNITY)"
    "$UNITY" \
        -quit -batchmode -nographics \
        -projectPath "$ROOT/client" \
        -buildTarget WebGL \
        -executeMethod Internal.WebGlBuild.Build \
        -logFile - | tail -n 40
fi

if [[ ! -f "$BUILD_DIR/index.html" ]]; then
    echo "No WebGL build at $BUILD_DIR — pass --build, or build from the editor (Tools/Build/WebGL)." >&2
    exit 1
fi

TAG="${TAG:-$(date +%Y%m%d-%H%M%S)-$(git -C "$ROOT" rev-parse --short HEAD)}"
RELEASE="$DEPLOY_PATH/releases/$TAG"

echo "==> Uploading $(du -sh "$BUILD_DIR" | cut -f1) to $DEPLOY_HOST:$RELEASE"
remote "mkdir -p '$DEPLOY_PATH/releases'"

# nginx.conf is bind-mounted straight from this directory (see
# client/deploy/docker-compose.yaml), so it ships with the deploy. It is only a
# couple of KB — always send it and let rsync report whether it actually changed.
conf_changed="$(rsync -ai --chmod=F644 -e "${SSH_CMD[*]} ${SSH_OPTS[*]}" \
    "$ROOT/client/deploy/nginx.conf" "$DEPLOY_HOST:$DEPLOY_PATH/nginx.conf")"

# --link-dest hardlinks unchanged files against the live release, so a redeploy
# only transfers the payloads that actually changed.
# --chmod: Unity writes the build with 0600, which nginx (running as its own
# uid inside the container) cannot read — without this every payload 403s.
rsync -az --delete --chmod=D755,F644 \
    -e "${SSH_CMD[*]} ${SSH_OPTS[*]}" \
    --link-dest="$DEPLOY_PATH/current" \
    "$BUILD_DIR/" "$DEPLOY_HOST:$RELEASE/"

# The link target is RELATIVE on purpose: the container mounts $DEPLOY_PATH at
# /srv, so an absolute host path inside the symlink dangles there and nginx 404s
# on everything.
#
# ln -sfn + mv -T: `ln -sfn` alone would drop the new link *inside* the existing
# `current` directory-symlink instead of replacing it. mv -T is the atomic swap.
echo "==> Switching current -> $TAG"
remote "cd '$DEPLOY_PATH' && ln -sfn 'releases/$TAG' current.tmp && mv -Tf current.tmp current"

echo "==> Pruning old releases (keeping $KEEP_RELEASES)"
remote "cd '$DEPLOY_PATH/releases' && ls -1t | tail -n +$((KEEP_RELEASES + 1)) | xargs -r rm -rf"

echo "Deployed $TAG"

if [[ -n "$conf_changed" ]]; then
    echo
    echo "nginx.conf changed — restart the web service in Coolify to pick it up."
fi
