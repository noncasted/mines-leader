#!/usr/bin/env bash
#
# Publishes a backend-only mirror of a source commit to the `deploy` branch.
#
# Why: Coolify clones the repo before every build (`git clone --depth=1 -b <branch>`), and the
# tip tree of `main` is dominated by the Unity client and art. The backend images need only
# backend/ + shared/ + the card icons of the console + .dockerignore + .editorconfig and the backend part of the framework submodule
# (tools/framework: backend/, shared/, common/, Directory.*.props, global.json) as plain files. Pointing Coolify at an orphan
# `deploy` branch that holds only those keeps the clone tiny; a shallow clone of one branch
# never touches the objects of the others.
#
# Paths are preserved, so nothing in the Coolify application config changes except the branch
# name: Base Directory stays `/`, Docker Compose Location stays
# `/backend/Tools/deploy/docker-compose.yaml`, and `context: ../../..` still lands on the root.
#
# Git LFS files inside the mirrored paths (none today) would be committed as real blobs and
# .gitattributes is not mirrored, so the branch builds with LFS off in Coolify.
#
# The branch is built with plumbing only — a temporary index, no worktree and no checkout — so
# the working tree and the current branch are never touched. Nothing is pushed until the very
# last command, and nothing is pushed when the mirrored content has not changed.
#
# The GitHub Actions workflow .github/workflows/sync-deploy-branch.yml does the same on every
# push to main; this script is for mirroring a commit that is not the tip of main, or when
# Actions is unavailable.
#
# Usage:
#   tools/scripts/sync-deploy-branch.sh              # mirror origin/main -> origin/deploy
#   tools/scripts/sync-deploy-branch.sh HEAD         # mirror another ref
#   DEPLOY_BRANCH=staging tools/scripts/sync-deploy-branch.sh
#   DRY_RUN=1 tools/scripts/sync-deploy-branch.sh    # build the commit, skip the push

set -euo pipefail

cd "$(dirname "$0")/../.."

SOURCE_REF=${1:-origin/main}
DEPLOY_BRANCH=${DEPLOY_BRANCH:-deploy}
REMOTE=${REMOTE:-origin}
DRY_RUN=${DRY_RUN:-}

# Directories copied wholesale, and single files copied from the repo root.
# docs/obsidian/game/cards/icons: ConsoleGateway.csproj ships the card icons (served at /card-icons).
DIRS=(backend shared docs/obsidian/game/cards/icons)
FILES=(.dockerignore .editorconfig)

# The backend part of the framework submodule, copied as plain files at the commit the source ref pins: the
# deploy branch has no submodules, so Coolify needs no access to the framework repository.
# client/core/Analyzers/Framework.Analyzers.dll is the one file of the Unity part the backend compiles with
# (backend/Directory.Build.props).
FRAMEWORK=tools/framework
FRAMEWORK_PATHS=(backend shared common Directory.Build.props Directory.Packages.props global.json client/core/Analyzers/Framework.Analyzers.dll)

LFS_POINTER_HEADER='version https://git-lfs.github.com/spec/v1'

source_sha=$(git rev-parse --verify "${SOURCE_REF}^{commit}")

# Refuse to mirror a ref that is missing a required path rather than silently publishing a
# tree that cannot build.
for path in "${DIRS[@]}" "${FILES[@]}"; do
    git cat-file -e "${source_sha}:${path}" 2>/dev/null \
        || { echo "error: ${path} does not exist in ${SOURCE_REF}" >&2; exit 1; }
done

framework_sha=$(git rev-parse --verify "${source_sha}:${FRAMEWORK}")
framework_git=$(cd "$(git rev-parse --git-path "modules/${FRAMEWORK}")" && pwd)
git --git-dir="${framework_git}" cat-file -e "${framework_sha}^{commit}" 2>/dev/null \
    || { echo "error: framework commit ${framework_sha} is not fetched: git submodule update --init ${FRAMEWORK}" >&2; exit 1; }

# The objects of the submodule become readable here (and are packed by the push) through the alternates.
export GIT_ALTERNATE_OBJECT_DIRECTORIES="${framework_git}/objects"

tmp_index=$(mktemp -t sync-deploy-index.XXXXXX)
trap 'rm -f "${tmp_index}"' EXIT
export GIT_INDEX_FILE=${tmp_index}
rm -f "${GIT_INDEX_FILE}"

for dir in "${DIRS[@]}"; do
    git read-tree --prefix="${dir}/" "${source_sha}:${dir}"
done

for file in "${FILES[@]}"; do
    # `ls-tree` gives the mode alongside the blob sha; --cacheinfo needs both.
    read -r mode _type blob _rest <<<"$(git ls-tree "${source_sha}" -- "${file}")"
    git update-index --add --cacheinfo "${mode},${blob},${file}"
done

for path in "${FRAMEWORK_PATHS[@]}"; do
    read -r mode type object _rest <<<"$(git ls-tree "${framework_sha}" -- "${path}")"
    [[ -n "${object:-}" ]] || { echo "error: ${FRAMEWORK}/${path} does not exist in framework ${framework_sha}" >&2; exit 1; }

    if [[ "${type}" == tree ]]; then
        git read-tree --prefix="${FRAMEWORK}/${path}/" "${object}"
    else
        git update-index --add --cacheinfo "${mode},${object},${FRAMEWORK}/${path}"
    fi
done

# Replace LFS pointers with their content. Pointers are tiny, so only small blobs are read.
lfs_count=0
while IFS=$'\t' read -r meta path; do
    read -r mode _type blob size <<<"${meta}"
    (( size < 1024 )) || continue
    [[ "$(git cat-file blob "${blob}" | head -n 1)" == "${LFS_POINTER_HEADER}" ]] || continue
    content_blob=$(git cat-file blob "${blob}" | git lfs smudge -- "${path}" | git hash-object -w --stdin)
    git update-index --cacheinfo "${mode},${content_blob},${path}"
    lfs_count=$((lfs_count + 1))
done < <(git ls-tree -r -l "$(git write-tree)")

tree=$(git write-tree)

git fetch --quiet "${REMOTE}" "${DEPLOY_BRANCH}" 2>/dev/null || true
parent=$(git rev-parse --quiet --verify "refs/remotes/${REMOTE}/${DEPLOY_BRANCH}" || true)

if [[ -n "${parent}" && "$(git rev-parse "${parent}^{tree}")" == "${tree}" ]]; then
    echo "Nothing to do: ${REMOTE}/${DEPLOY_BRANCH} already matches ${SOURCE_REF} ($(git rev-parse --short "${source_sha}"))."
    exit 0
fi

message="Deploy mirror of ${SOURCE_REF} @ $(git rev-parse --short "${source_sha}")

Generated by tools/scripts/sync-deploy-branch.sh. Do not commit here directly —
this branch is overwritten from main on every sync.

Source-commit: ${source_sha}"

commit=$(git commit-tree "${tree}" ${parent:+-p "${parent}"} -m "${message}")

echo "Mirrored $(git rev-parse --short "${source_sha}") -> ${commit:0:7} ($(git ls-tree -r "${tree}" | wc -l) files, ${lfs_count} LFS files materialised)"

if [[ -n "${DRY_RUN}" ]]; then
    echo "DRY_RUN set — not pushing. Inspect with: git ls-tree -r --name-only ${commit} | head"
    exit 0
fi

# Force: the Actions workflow publishes orphan commits, so the remote tip may not be an
# ancestor of ours. The branch is generated output, there is no history worth preserving.
git push --force "${REMOTE}" "${commit}:refs/heads/${DEPLOY_BRANCH}"
echo "Pushed to ${REMOTE}/${DEPLOY_BRANCH}."
