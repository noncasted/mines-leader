# Values of this project for the shared skills

The skills live in `tools/framework/.claude/skills`; what differs per game is here.

## deploy-client

`.claude/deploy-client.json`: one target, `web`.

| What | web |
|------|-----|
| Build profile | `Assets/Settings/Build Profiles/web_dev - Desktop - Development.asset` |
| Build output | `builds/web` of the copy of the run (gone after the run unless `KEEP_COPY=1`) |
| Stripping settings | `Assets/Settings/DefaultSubmoduleStrippingSettings.asset` |
| Tests | `tools/scripts/run-tests.sh` (backend, container generator, Unity EditMode) |
| Deploy | `tools/scripts/deploy-webgl.sh` with `BUILD_DIR` of the run (`--list`, `--rollback <tag>`, no target arguments) |
| Deploy details | `client/deploy/README.md` |

The web build is served at `https://minesleader.xyz/` by an nginx container on the Coolify host (service `client`,
application `web`); `nginx.conf changed` in the output of the deploy script means that application needs a restart in
Coolify. The link is printed as `URL: ...` when `PUBLIC_URL` is set in `deploy.env`.

The backend is not deployed by this skill: the Coolify application `backend` builds the orphan branch `deploy` by webhook;
`.github/workflows/sync-deploy-branch.yml` mirrors the backend part of `main` into it on a push that touches the backend
(`backend/Tools/deploy/COOLIFY.md`, "Deploy branch"). A client with a new protocol goes out only after that push.

Hooks of `.claude/deploy-client.json`:
- `beforeUpload` (`tools/scripts/deploy-client-backend-gate.sh`): waits (`BACKEND_WAIT`, 900 s) until the live meta
  (`https://meta.minesleader.xyz`) accepts `ProtocolHash.Value` of the tree: a websocket closed with 4001 is a backend on
  another protocol. A client ahead of the backend makes every player see "client outdated" (2026-11-17: about 10 minutes
  between the backend of framework 0.9.3 and its client).

Backend of a build: a player always talks to `_productionApiUrl` of `OptionsContainer.asset` (`BackendOptions.Url`:
`_environment` counts only in the editor), so every build of the profile goes to `https://meta.minesleader.xyz`. A build
for the local cluster: put `http://localhost:7101` into `_productionApiUrl` for the moment of the snapshot (the line
`Snapshot ready` of the run), then restore it; never commit it. Such a build is checked on `http://localhost:<port>/`
(a secure context, the Cache API works) with nginx of `client/deploy/nginx.conf`.

Tests and the shared Unity lock: `run-tests.sh` takes `UNITY_LOCK` (default `/tmp/claude-1000/unity-batchmode.lock`),
the variable passes through `deploy-client.sh`; the builds take only the build slots (`unity-build.slot<i>.lock`).

Content check of `checks web`: since framework v0.9.1 `verify.sh` looks for the bundle names in the catalog
(`Resources/Content/catalog.json`) unpacked from `data.unity3d` (UnityFS, LZ4 blocks) by `deploy-client/webdata.py`;
needs `python3`. On v0.9.0 it grepped the packed `.data` and failed with `the catalog in the .data file does not name
<bundle>` for every bundle not preloaded. Run of 2026-11-17 on v0.9.1: all stages PASS, `content 9 bundle files
(2 preloaded)`.

Credentials: `~/.config/mines-leader/deploy.env`, `tools/scripts/.deploy-password`.

## commit

Scopes of this game for `commit`, `branch-commit`, `split-commit`: [Client], [Backend], [Shared], [Console], [Infra],
[Tests], [Cards], [Claude], [Docs], [Misc]. [Cards] is a card of the game across the codebases (shared model, backend
mechanic and bot strategy, console config, client action), as in `[Cards] Add Lockdown card`.

## workflow

The docs of this game for the keyword table of the task brief are in the table "Keyword -> Documentation" of `CLAUDE.md`:
`.claude/docs/GAMEPLAY.md` (game flow, board, cards, bots, matchmaking), `.claude/docs/PREFAB_CODEGEN.md` (PrefabBuilder),
`.claude/docs/CARD_EFFECTS.md` (card effects).
