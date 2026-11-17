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

## client-telemetry

- url: `https://meta.minesleader.xyz`
- env-file: `~/.config/mines-leader/deploy.env`

The tags of the sessions of this game: `menu` (the first one opens after the authorization of the meta socket, the next ones
before the menu loads) and `match` (before the scope of a match loads; the mark `match.started` splits the loading and the
wait for the players from the play). A rematch opens a new `match` session. The startup trace is sent after every
authorization of the meta socket. Details: `.claude/docs/TELEMETRY.md`.

The local cluster (Aspire): `--url http://localhost:7101` (MetaGateway) and an env file with
`CONSOLE_TOKEN=local-dev-token` (`ConsoleToken` of `backend/Orchestration/Aspire/appsettings.json`; in Development the
check is off for the development token, any line works); a local `backend/Orchestration/Aspire/appsettings.local.json` may override `ConsoleToken`, then the
check is on and the env file takes that value). The telemetry is off in the editor by default
(`ClientTelemetryOptions`): a local check needs a WebGL build or the option turned on for the time of the check.
The same section gives the address and the token to the skill `match-history`.

## commit

Scopes of this game for `commit`, `branch-commit`, `split-commit`: [Client], [Backend], [Shared], [Console], [Infra],
[Tests], [Cards], [Claude], [Docs], [Misc]. [Cards] is a card of the game across the codebases (shared model, backend
mechanic and bot strategy, console config, client action), as in `[Cards] Add Lockdown card`.

## workflow

The docs of this game for the keyword table of the task brief are in the table "Keyword -> Documentation" of `CLAUDE.md`:
`.claude/docs/GAMEPLAY.md` (game flow, board, cards, bots, matchmaking), `.claude/docs/PREFAB_CODEGEN.md` (PrefabBuilder),
`.claude/docs/CARD_EFFECTS.md` (card effects).

## device-lab

- Game values: `.claude/device-lab.json` (host, meta host). No stored build, no autorun query, no scenario API yet: the options
  `--autorun` and the `call` steps of a stand scenario do not apply.
- A build for the stand: `KEEP_COPY=1 tools/framework/tools/scripts/deploy-client.sh web --no-deploy --notest`, then copy
  `builds/web` of the copy of the run out and pass its folder to `--build` / `--variant`. Its meta is the live
  `https://meta.minesleader.xyz` (§ deploy-client), which the stand cuts.

## visualize

- Pixel art in Aseprite, palette Apollo (`client/Assets/Art/apollo-32.aseprite`, `client/Assets/Art/Editor/apollo.colors`):
  no colours outside it unless the user asks. Art of the game: `client/Assets/Art/Game` (cards, cells, field, UI, vfx),
  card icons in `client/Assets/Art/Cards`.
- Real size: sprites at 12 PPU; menus are drawn at the art-native 512x288. Fonts: `BITACH` for card names, `DreiFraktur` for
  the round timer, `Ithaca-LVB75` for the rest (`.claude/docs/CLAUDE_MISTAKES.md`).
- Generators: none draws the look; `CardIconsExporter` (`client/Assets/Common/Internal/Editor/Tools`) only exports the card
  icons as PNG, useful as the "before" of a card variant.
