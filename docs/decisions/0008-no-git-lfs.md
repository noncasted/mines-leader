# 0008. No Git LFS

Status: accepted.

## Problem

The repository holds binary art (PNG, Aseprite, WAV, WebP) and its `.git` is large. Git LFS is the usual answer, and
its absence looks like an oversight.

## Decision

Keep binaries in plain git. Reasons:
- The size of `.git` is history, not the current tree. LFS applies to new commits only; it would not shrink what is
  already packed unless the history is rewritten, which changes every commit hash (the docs and the task notes refer
  to commits by hash) and breaks every existing clone.
- GitHub meters LFS storage and bandwidth per account: every clone and every CI checkout that downloads an LFS
  object counts against the quota. Plain git objects do not.
- The current tree is small: the binaries an artist changes are a few tens of MB.

## Numbers

Measured on 2026-11-25 (`git count-objects -vH`, `git rev-list --objects --all` with `git cat-file --batch-check`,
compressed size in the pack):

| What | Size |
|---|---:|
| Pack of the whole history | 272 MiB |
| Executables of the embedded Addressables package (`AddressablesFormatConverter` for 4 platforms), deleted since | 100 MiB |
| WebGL builds once committed to `client/Deploy`, deleted since | 55 MiB |
| Current tree at `HEAD`, everything, uncompressed | 61 MiB |
| Binary art and media at `HEAD` (png, aseprite, wav, mp3, ogg, webp, fonts) | 32 MiB |

More than half of the pack is two kinds of files that are no longer in the tree; LFS would have kept neither of them
out of history after the fact. The CI checks out with `fetch-depth: 1`, so it does not download that history at all.

## Cost

- A full clone downloads 272 MiB.
- Every new version of a binary stays in the history forever; a large asset changed often (music, a big sprite sheet)
  would grow the pack quickly. Generated outputs (builds, packages with native tools) must stay out of the repository:
  they are the ones that grew it.
- If binaries ever grow past that, the move is LFS for the new paths plus a history rewrite, done once and on purpose.
