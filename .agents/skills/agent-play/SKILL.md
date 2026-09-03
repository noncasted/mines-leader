---
name: agent-play
description: Play a LastManStandingTurnBased vs-bot match through Unity MCP using tools/scripts/game-agent.py. The script sets GameMock._mode to 31, applies an optional hand/board fixture and enters Play over HTTP MCP. Every turn the agent decides explicitly about each card in hand (play where and why, or skip why) before opening cells. Use this skill whenever the user runs /agent-play, asks to play as the agent, start agent play, сыграть агентом, запустить agent play, vs-bot MCP match, проверь карту X агентом, or to take turns with game_open / game_use_card / game_end_turn. Do not start the Aspire cluster or Unity MCP — the user brings those up by hand.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /agent-play, agent-play, agent play, сыграй агентом, запусти agent play, играй vs бота, проверь карту агентом, game-agent, game_open, game_start_vs_bot
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Agent Play

Sit in the human seat of a `GameMatchType.LastManStandingTurnBased` (31) vs-bot match and take turns through `tools/scripts/game-agent.py`. The point of agent play is to exercise cards: a match where the hand stays untouched is a failed run.

The user starts the Aspire cluster and Unity MCP (Editor window on `:8080`) themselves. Do not run `/start-cluster`. Do not launch Unity. Do not launch `mcp-for-unity` / `uvx mcp-for-unity`. If either is down, stop and say what is missing.

Moves, mode, fixture, and Play go only through `game-agent.py`. Do not invent Unity MCP `game_*` calls and do not drive `manage_editor` / `execute_code` via Grok `search_tool` — Coplay HTTP often hides parameterized tools from `tools/list`, and the Grok `unityMCP` handshake often fails while HTTP `:8080` still works. The script already talks to that HTTP endpoint.

Repo root: `/projects/mines-leader`

```bash
python3 tools/scripts/game-agent.py <cmd>
```

Override URL with `$GAME_AGENT_MCP_URL` (default `http://127.0.0.1:8080/mcp`).

## Arguments

Default: start (or attach) and play until game over.

| User says | Do |
|-----------|----|
| `/agent-play` | Full match |
| `start` / `только старт` / `подключись` | Attach, print `status` + `state`, stop |
| `один ход` / `one turn` | One own turn, then stop |
| `--scenario NAME` | `start --scenario NAME` (JSON in `tools/scripts/agent-scenarios/`) |
| `--bot Easy\|Medium\|Hard`, `--hand T1 T2`, `--deck`, `--bot-deck`, `--board`, `--human-first`, `--mana`, `--moves` | Pass through to `start`; the fixture is applied at match creation |
| `проверь карту Trebuchet` / `test card X` | `start --hand X X --human-first`, then play until the card has been used at least once and report what it did |

## Step 1 — Preconditions (one command, start nothing)

```bash
python3 tools/scripts/game-agent.py preflight
```

One line: `cluster 200 | mcp ok match_active=no | silo started ... | sources fresh`. Exit 0: go to Step 2 immediately. Exit 1 prints `FAIL ...` lines: stop and relay them to the user verbatim. The three failures and what to say:

- cluster not 200: the user starts it (`start-cluster` skill). Never start it yourself.
- MCP unreachable: the user connects MCP for Unity in the Editor (HTTP `127.0.0.1:8080`). Never start it yourself.
- `STALE cluster`: `backend/` or `shared/` changed after the Silo process started; the server plays with old rules. Ask for a cluster restart. `--ignore-stale` only when the user says so.

Do not check any of this by hand (`curl`, `ps`, `stat`, `git status`): `start` runs the same preflight again and refuses on failure. Do not ask the user to set the mode or press Play. Do not skip this because Grok has no Unity Editor tools in the session.

## Step 2 — Enter the match

```bash
python3 tools/scripts/game-agent.py start        # plain /agent-play: no fixture
python3 tools/scripts/game-agent.py turn         # waits for turn_start, prints state + legal plays + solver
```

That is the whole entry: three commands from the user prompt to the first card decision. Do not list scenarios, do not read `tools/scripts/agent-scenarios/*.json`, do not weigh which fixture "exercises cards better": a plain `/agent-play` is a full match with a random hand. A fixture is used only when the user named a scenario, a hand, a board, or a card to test:

```bash
python3 tools/scripts/game-agent.py stop                          # a fixture needs a fresh Play; "already_stopped" is fine
python3 tools/scripts/game-agent.py start --scenario cross_vs_hard # or --hand ... --bot ... --board ... --human-first
python3 tools/scripts/game-agent.py turn
```

`start` runs preflight, then `ensure-play`: load `Assets/GamePlay/Scenes/Game_Field.unity` if needed, set `GameMock._mode` to `LastManStandingTurnBased` (31) if it is not, write the fixture JSON into `EditorPrefs` (`GameMock.FixturePrefsKey`, read once and deleted by `GameMock`), `manage_editor play` (stop + replay if already playing with the wrong mode). Then it waits for `GameAgentBridge` and prints `active: true` plus the `fixture` it wrote. `start` returns before the round starts; `turn` brings the first observation. `match_active=yes` in preflight means a match is already running: `start` attaches to it; `stop` first only when a fixture was requested.

Fixture rules:

- `--hand` / `--deck` / `--bot-deck` take `CardType` names. Short decks are fine: the server repeats them until the deck covers `HandSize`.
- `--human-first` gives turn 1 to the agent; attack cards (`Target: OpponentBoard`) are then unplayable on turn 1 (`Opponent board is not generated yet`) until the bot opens its first cell. ZipZap is unplayable on an untouched own board. That is the rule, not a bug.
- `--board` is the own board layout in `BoardParser` alphabet; `--bot Easy|Medium|Hard`; `--mana N`; `--moves N`.
- Scenarios live in `tools/scripts/agent-scenarios/*.json` (`scenarios` lists them) and take the same keys.

### When start does not produce a turn

`start` says `active: true` but `turn` times out and `state` returns `No observation yet` while Unity shows `All players connected. Starting the match...`: the server round died during init. Look before retrying:

1. `backend/.telemetry/logs-games/<UTC date>/<time>_<sessionId>.log` — the newest file. A log that stops right after the `[Buff] Applied` lines means round init threw after the base buffs (fixture apply, card draw). Round exceptions are not written there.
2. `backend/.telemetry/logs/gamegateway.log` — `Creating match` / `Session ... created` confirm the match reached the server; timestamps are UTC.
3. Unity Console (user side): `[GameMock] Applying agent fixture: {...}` confirms the `EditorPrefs` key was read; no such line means the key was not written before Play.

Report the finding with the session id. Do not loop `start` hoping it fixes itself; `stop` first.

## Step 3 — Play a turn

Repeat until `GameOver` or the user asked for one turn. There is no other stop condition: "several full turns are enough" is not a reason to stop. Cap: 40 own turns, then stop and report.

**Mana rule.** Mana refills at the end of every turn. Unspent mana is lost. A card left in hand with affordable cost is a wasted turn unless you write why it is worse than nothing. Moves refill too, but moves are for cells; mana is only for cards.

**Match rules.** The goal of the run is to play every card in hand and report what it did, not to win. The match ends when a player's Health reaches 0 or when a player has flagged every mine on their own board with no wrong flags (WinReason `Player <id> flagged every mine on own board`; solver `FLAGS` placed every turn gets there by itself). Health drops only when a mine explodes on that board (own `open` / `chord` on a mine, `OpponentBomb` / `ChainReaction` on an enemy mine). Do not plan around damage: if `OpponentBomb` / `ChainReaction` are in hand and affordable, play them at the `best` proven mine like any other card; if they are not, damage is not a topic. Every other card (`Trebuchet`, `Smoke`, scouts, fog, frost) is played for its own effect and never "to set up damage". The bot heals with `Medic` on its turn; that is expected, not something to counter. Cards cost mana only, never moves.

**Tool rules.** The script prints compact text; read it as is. Do not pipe it into your own `python3 -c` / `jq` filters and do not pass `--json` unless another script needs the payload. Do not read backend or client source during play. If a tool answer looks ambiguous (a card with `no legal cell` and no error, a strange trigger), treat it literally and keep playing; note it for the report.

### 3.1 Look

```bash
python3 tools/scripts/game-agent.py turn
```

One call: waits for `turn_start` (or game over), then prints

- header: `seq`, `trigger`, `own_turn`, `game_over`; resources of both players (`deck` = cards left to draw, `stash` = discard pile, only `Gravedigger` reads it); `hand:` as `Type(cost Target Shape/Size)`; `events`; both boards as ASCII with x/y rulers (digit = open cell with mines around, `.` closed, `F` flag, `*` exploded, `~` fog);
- `LEGAL PLAYS`: per card `cost`, target, shape, `cells N` and `best x,y(...)` — the best solver centers that are also legal for that card (scout: unknown closed cells; attack: open or closed cells by card; OpponentBomb / ChainReaction: proven enemy mines, then highest `p_mine`; DimensionRift: `x,y(opp open 4, own open 0, own flags 0)`, ranked by what the swap takes from the enemy minus what it destroys on your side, so the first `best` never trades away your flags when a clean area exists). `ERR ...` means the server refuses the card now (`Opponent board is not generated yet` until the bot opens a cell; `Own board is not generated yet: open a cell first` for ZipZap on turn 1). Recycler lists `--extra-card-id` choices with types; ZipZap needs no extra card.
- `SOLVER`: `SAFE OPENS`, `CHORDS`, `FLAGS`, `GUESSES` (only when nothing is proven), `SCOUT TARGETS`, `ZIPZAP TARGETS`, `ATTACK TARGETS` (target sections appear only for card types in hand). `BOARD NOT GENERATED` (turn 1 with `--human-first`): mines are placed after the first open, so any cell is safe; run the `open` it prints and re-solve. Scout cards on that board show `own board not generated ...` in LEGAL PLAYS: they are legal (the card generates the board around the click) but pointless; open first, then play them.
- `events`: only card plays of both sides (`[Card] Used`, `[Bot] Card | Used Medic`), what a card did to a board (`[Card] Effect | Player=Bot | Card=Bloodhound | Defused=2 Opened=7`), opened cells, health, buffs, game over. Read them: they explain why the enemy Health went back up or where its mines exploded.

The `cards:` block under the hand line (one catalog summary per card type) is the card reference. A card with `no position needed, playable now` in LEGAL PLAYS has no condition: it is playable right now. The only conditions that exist are written in the summary (`Fails if ...`); do not invent others. Do not look cards up anywhere else; "I do not know this card" is not a valid reason. `state` alone re-prints the observation, `legal-plays` alone re-prints legal plays with `best`, `solve` alone re-prints the solver (`solve --section flags` answers "any proven flags left?" in one line before the last move). All of them are `game-agent.py` subcommands; `tools/scripts/board-solver.py` is a separate offline script you do not need during play. Use those only mid-turn after the board changed; do not call them twice in a row without an action in between.

### 3.2 Card phase (before any open)

Write one line per card in hand, in this exact format, then act on it:

```
Type: play @x,y — reason
Type: play — reason                 (no position)
Type: skip — reason
```

Allowed skip reasons: `cost > mana`, `legal-plays error: …`, `no legal cell`, `no useful target (solver: …)`, `saving for guess (Shield)`, `HP full (Medic)`, `no cards to discard (Recycler)`, `Dud`, `deferred: mana went to <Type> this turn`. `cost` in the hand line and in LEGAL PLAYS is the effective cost with your discounts and penalties; a card you cannot afford shows `ERR Not enough mana: N needed, M left` in LEGAL PLAYS and the server refuses it. Rule for the last two: `cost > mana` is only true when `mana < cost` at the moment you write the line; if `mana >= cost` and you still do not play the card, the reason is `deferred`, whatever you spent the mana on. Anything else is not a skip reason; pick a cell and play. Do not invent a reason to fit the list: if the card is affordable and has a `best` cell, play it there.

Priority when mana is short:

1. Resources: `Overclock`, `Adrenaline`, `ManaSurge`, `BloodPact`, `ManaFountain`, `Focus`, `PowerSurge`, `DoubleOrNothing`, `GamblersRuin`, `CoinToss`.
2. Scout own board: `Sonar`, `MinefieldScout`, `Bloodhound`, `Excavator`, `ThermalVision`, `ChaosDiamond`, `ChaosScout`, `ZipZap`, `ErosionDozer`, `FortuneCookie` at the first `best` cell of the card. Prefer the pocket where the solver has no safe opens.
3. Attack: `Trebuchet`, `MineCluster`, `CarpetBomb`, `FortuneBlast`, `Smoke`, `FogOfWar`, `Frost`, `Blackout`, `ChaosFog`, `OpponentFlagErase`, `OpponentFlagReshuffle`, `DimensionRift`, `OpponentBomb`, `ChainReaction` at the first `best` cell of the card. `Siphon`, `Lockdown`, `Embargo`, `HandScramble`, `CardThief`, `SabotageDeck`, `SoulLink` whenever affordable.
4. Draw: `Scavenger`, `Salvage`, `Recycler` (with `--extra-card-id`), `MysticDraw`, `Gravedigger`, `MirrorMatch`.
5. Defense: `Shield` when a guess is coming this turn, `Medic` when `Health < HealthMax`, `Purge` when own cells carry enemy effects.

Play, one command per card, always by `--type` (ids change after every draw):

```bash
python3 tools/scripts/game-agent.py use-card --type Sonar --x 5 --y 7
python3 tools/scripts/game-agent.py use-card --type Medic
python3 tools/scripts/game-agent.py use-card --type Recycler --extra-card-id <guid from LEGAL PLAYS>
```

Every action prints one line: `ok ... | hp 3 mana 5->3 moves 5 | self: opened 4 | opp: closed 12 mines 40->54 | hand: -Trebuchet` plus new events. Counters name what the card did: `mines 40->54` on `opp:` means mines were planted on the enemy board, `flags` counts flags, `fog cells 0->9` / `blackout cells` / `frost cells` count cells under an effect. A card play adds a second line `Type: summary` with the catalog text of the card that left the hand; that plus the counters is the card result for the report, no guessing from side effects. `hand: played GamblersRuin drew A B C` / `discarded X Y` names the branch of a coin-flip or draw card. `own mines 40->39 removed without damage (hp 3)` means a scout detonated or defused own mines safely. `own board changed by the card: ... stale` after `Bloodhound` / `ZipZap` / `DimensionRift`: re-run `legal-plays` / `solve` before the next positional card. `opponent board changed by the card ...` after an area attack means the same for attack cards: the next `Trebuchet` needs a fresh `best`. `ERR <text>` means the action did nothing: keep the card as `skip — legal-plays error: <text>` for this turn and continue. After a card that draws or discards (`GamblersRuin`, `Recycler`, `Scavenger`, `MysticDraw`, `HandScramble` on you) the result adds a `hand now:` line with the current hand; the `turn` header is stale from then on. Run `legal-plays` before the next card for `best` cells. The `mana 6->4/6->8` part is current/max, each with its own `old->new` when it changed: a `MaxMana` buff (`GamblersRuin`) raises the max, and the budget for the rest of the turn is the current value. After a scout card, run `solve`: the board changed.

### 3.3 Minesweeper phase

Act, then re-solve. Do not dump five random guesses in one turn.

- `flag X Y` for every solver `FLAGS` entry (legal on opponent turn too; do it on own turn). Flags cost no moves.
- `chord X Y` for solver `CHORDS`, `open X Y` for `SAFE OPENS`. Each costs one move. Before the move that takes `MovesLeft` to 0, play every card you still want to play: the round closes on that move.
- Several safe moves may go in one shell line separated by `;` (not `&&`: one `ERR` must not hide the rest). Read every `ok` / `ERR` line. Every `open` / `chord` / `flag` is checked against the state saved after the previous action before it is sent: a stale target is refused locally (`stale: 5,6 has no closed neighbours left (state seq 83); run solve for a fresh list`, `chord 7,5 needs 2 flag(s) around, 1 placed`). Such a refusal costs nothing, but it means the rest of your batch is stale too: stop and `solve`. Never put more `open` / `chord` in one line than `moves` in the last result line: the extra ones are refused (`ERR No moves left`, or the script's `MovesLeft is 0` guard) and the round has closed under them.
- A cascade (`self: opened N` with N > 1) makes the rest of the `CHORDS` / `SAFE OPENS` list stale: the cells may be open already. Re-solve before the next chord instead of finishing the batch. A chord with nothing to reveal or with wrong flag count is refused (`ERR Chord reveals nothing`, `ERR Chord needs N flag(s)`) and costs no move.
- If no safe opens/chords/flags and `MovesLeft > 0`: if `Shield` is in hand and affordable, play it now. Then take **one** solver `GUESSES` entry (lowest risk) and re-solve. Do not chain guesses. Never "prove" cells by hand instead of the solver.

### 3.4 Second card pass and end

If mana is left and a card in hand has a `best` cell, run 3.2 once more (scout cards are stronger after opens reshaped the frontier). Then end the turn by one of two ways, never both:

- `MovesLeft == 0` after the last `open` / `chord`: the server closes the round itself (the script refuses any further move with `MovesLeft is 0`). Run `turn` and act on the next `turn_start`. `end-turn` is not needed here; it exists only for the second case.
- `MovesLeft > 0` but nothing useful remains: `end-turn` (it is `SkipTurn`, it terminates the current round immediately), then `turn`.

```bash
python3 tools/scripts/game-agent.py turn      # after the last move
python3 tools/scripts/game-agent.py end-turn  # only with moves left, then turn
```

`turn` / `wait` skip the stale `action` frames with `IsOwnTurn: true`, `MovesLeft: 0` that the bridge hands out while the bot plays; they return only on `turn_start`, game over, or an own turn with moves left. Refuse to `open` / `chord` / `use-card` / `end-turn` off-turn — the bridge errors `Not your turn`. Flags are allowed off-turn; do not sit on the opponent turn waiting to flag unless you already have proven flags.

If a command returns `ERR`, stop that action, print the error, and continue the turn if still alive.

## Step 4 — Report

Russian prose. Identifiers in English.

```
## Agent play

Match: active | game over
Winner / WinReason: …                 (`Player <id> flagged every mine on own board` = that player fully flagged their OWN board)
Fixture: none | hand=[…] bot=… board=… (verified: yes/no)
Turns taken: N
Last trigger: turn_start | action | opponent_turn | game_over

Cards:
- turn 1: Sonar @5,7 → flagged 3 (events: …) | skipped Trebuchet (legal-plays error: Opponent board is not generated yet)
- turn 2: Trebuchet @9,3 → opponent closed 12, mana 5→3; skipped Medic (HP full)
- unplayed at game over: [Shield]

What happened:
- flags / opens / chords / guesses this session
- health now
- why we stopped (game over / one turn / precondition / cap)
```

`Cards` is mandatory: one line per own turn, every hand card either played (position, action line result) or skipped (reason from the allowed list). A run of 5+ turns should show at least 3 different card types played; if it does not, say why (no mana, no legal cells, hand of Duds).

Do not dump full `Cells` arrays or raw JSON. `BoardAscii` plus a short action list is enough.

## Commands

| CLI | MCP tool |
|-----|----------|
| `preflight` | cluster 200 + `game_status` + stale-binaries check; `start` runs it too |
| `status` | `game_status` |
| `ensure-play [fixture args]` | `execute_code` (set `GameMock._mode` = 31, write `EditorPrefs` fixture) + `manage_editor` play |
| `start [fixture args]` | `ensure-play` then `game_start_vs_bot` |
| `stop` | `manage_editor` stop |
| `scenarios` | list `tools/scripts/agent-scenarios/*.json` |
| `turn` | `game_wait_turn` until `turn_start` / game over, then `game_get_state` + `game_legal_plays` + solver, one printout |
| `state` / `state --oracle` | `game_get_state`, compact text |
| `wait` | `game_wait_turn` until `turn_start` / game over / own turn with moves left |
| `diff` | `game_get_state` vs the last saved observation, no match changes |
| `open X Y` | `game_open`, prints one result line |
| `chord X Y` | `game_chord`, prints one result line |
| `flag X Y` / `unflag X Y` | `game_flag` / `game_unflag`, prints one result line |
| `use-card --type T [--x N --y N] [--extra-card-id G] [--chosen-index I]` | `game_use_card`, prints one result line |
| `solve [--section all\|flags\|opens]` | `game_get_state` + solver, trimmed to the hand; `flags` prints one line |
| `legal-plays` | `game_legal_plays` + solver `best` cells |
| `inspect X Y` | `game_inspect_cell` |
| `wait-visual` | `game_wait_visual` |
| `end-turn` | `game_end_turn`; refused when `MovesLeft` is 0 |
| `--json <command>` | raw payload (full observation with `Cells`) for other scripts only |

`--oracle` only works when coordinator `LastManStandingTurnBased.IncludeOracle` is true (default false).

`tools/scripts/card-advisor.py` is optional: an external LLM second opinion that needs an OpenCode key. Use it only when the user asks for cards-via-advisor; the decision stays yours.

## Do not

- Start Aspire, Docker Postgres, Unity Editor, or the MCP server.
- Use `GameMatchType.LastManStanding` (30) or `TimeLimited` — no agent observation.
- Treat `solve` output as if it sent moves. It only suggests.
- End a turn with affordable cards in hand and no written skip reason.
- Call `end-turn` after the last move took `MovesLeft` to 0. The round is already closed; `end-turn` skips the next own round instead.
- Stop before `GameOver` on your own judgement ("enough turns", "the bot seems stuck"). Play on, or report the tool error that blocks you.
- Read backend / client source, session logs, or `game-agent.py` internals during play. The only allowed log look is Step 2 "When start does not produce a turn".
- Pipe script output through your own JSON filters, or call `state` / `legal-plays` / solver repeatedly without an action in between.
- Play a card by id: use `--type`.
- Golden screenshots / pixel diff. `inspect` is semantic Unity cell state.
- Wrap `GameCheatsBridge`.
