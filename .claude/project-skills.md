# Values of this project for the shared skills

The skills live in `tools/framework/.claude/skills`; what differs per game is here.

## deploy-client

Not finished: `.claude/deploy-client.json` (targets for `tools/framework/tools/scripts/deploy-client.sh`) is added by a
later step of the framework 0.9.0 update. Without it the script exits 2: stop and say so.

The values of the old copy of the skill (one target, web):

| What | web |
|------|-----|
| Build profile | `Assets/Settings/Build Profiles/web_dev - Desktop - Development.asset` |
| Build output | `client/build` (Unity path: `build`); `client/Build` must not hold a build next to it |
| Stripping settings | `Assets/Settings/DefaultSubmoduleStrippingSettings.asset` |
| Tests | `tools/scripts/run-tests.sh` (backend, container generator, Unity EditMode) |
| Deploy | `BUILD_DIR=client/build tools/scripts/deploy-webgl.sh` (`--list`, `--rollback <tag>`) |
| Deploy details | `client/deploy/README.md` |

The web build is served by an nginx container on the Coolify host at `/`; `nginx.conf changed` in the output of the deploy
script means the web service needs a restart in Coolify. The link is printed as `URL: ...` when `PUBLIC_URL` is set in
`deploy.env`.

Credentials: `~/.config/mines-leader/deploy.env`, `tools/scripts/.deploy-password`.

## commit

Scopes of this game for `commit`, `branch-commit`, `split-commit`: [Client], [Backend], [Shared], [Console], [Infra],
[Tests], [Cards], [Claude], [Docs], [Misc]. [Cards] is a card of the game across the codebases (shared model, backend
mechanic and bot strategy, console config, client action), as in `[Cards] Add Lockdown card`.

## workflow

The docs of this game for the keyword table of the task brief are in the table "Keyword -> Documentation" of `CLAUDE.md`:
`.claude/docs/GAMEPLAY.md` (game flow, board, cards, bots, matchmaking), `.claude/docs/PREFAB_CODEGEN.md` (PrefabBuilder),
`.claude/docs/CARD_EFFECTS.md` (card effects), `.claude/docs/UI_MENU.md` (menu UI Toolkit, palette),
`.claude/docs/CODE_EXAMPLES.md` (runnable examples in `client/Assets/Common/Docs/Claude`).
