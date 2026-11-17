# Trigger Keywords → Documentation Finder

**Read this file first when starting code review. Check for keywords below and follow the recommended reading order.**

---

## REACTIVE & STATE MANAGEMENT

**Keywords:** reactive, event, subscription, property, collection, observable, advise, view, change detection, state change, notify, listener

### Recommended Reading Order
1. **START HERE:** `tools/framework/.claude/docs/COMMON_REACTIVE_BASICS.md` – EventSource, basic events
2. **If handling values:** `tools/framework/.claude/docs/COMMON_REACTIVE_VALUES.md` – ViewableProperty, LifetimedValue
3. **If handling collections:** `tools/framework/.claude/docs/COMMON_REACTIVE_COLLECTIONS.md` – ViewableList, ViewableDictionary
4. **For patterns:** `tools/framework/.claude/docs/COMMON_REACTIVE_PATTERNS.md` – Real-world usage

### Common Mistakes
- ❌ Subscribing without Lifetime → memory leak
- ❌ Using `Advise()` when you need `View()` → missing initial state
- ❌ Mixing EventSource with ViewableProperty → wrong pattern for values

---

## LIFETIME & RESOURCE MANAGEMENT

**Keywords:** lifetime, resource cleanup, termination, IReadOnlyLifetime, scope, disposal, parent-child, token

### Recommended Reading Order
1. **START HERE:** `tools/framework/.claude/docs/COMMON_LIFETIMES.md` – Lifetime basics, creation patterns
2. **For patterns:** `tools/framework/.claude/docs/COMMON_LIFETIMES_PATTERNS.md` – Advanced scenarios

### Common Mistakes
- ❌ Lifetime never terminates → memory leak
- ❌ Ignoring lifetime in callbacks → untracked subscriptions
- ❌ Not using parent-child hierarchy → manual cleanup needed

---

## DEPENDENCY INJECTION & SERVICES

**Keywords:** MonoBehaviour, ISceneService, IScopeSetup, Create, OnSetup, [Inject], VContainer, registration, DI, service, scope builder

### Recommended Reading Order
1. **START HERE:** `tools/framework/.claude/docs/COMMON_CONTAINER.md` – DI fundamentals, MonoBehaviour registration

### Common Mistakes
- ❌ Forgot `ISceneService` + `IScopeSetup` → `OnSetup()` never called
- ❌ Forgot `Create()` method → component not registered
- ❌ Initialization in `Awake()` instead of `OnSetup()` → timing issues
- ❌ Forgot to register in `Create()` → component invisible to scope

### Quick Checklist
- [ ] Class implements `MonoBehaviour, ISceneService, IScopeSetup`
- [ ] Has `public void Create(IScopeBuilder builder)` method
- [ ] Calls `builder.RegisterComponent(this).As<IScopeSetup>()`
- [ ] Has `public void OnSetup(IReadOnlyLifetime lifetime)` method
- [ ] All subscriptions/listeners registered with `lifetime` parameter

---

## CODE STYLE & PATTERNS

**Keywords:** member order, field naming, method structure, local function, GC.KeepAlive, braces, logging, error handling

### Recommended Reading Order
1. **START HERE:** `tools/framework/.claude/docs/CODE_STYLE_FULL.md` – Organization, naming, structure

### Common Mistakes
- ❌ Wrong member order (fields before constructor) → harder to read
- ❌ Abbreviated names (`_obs` instead of `_observers`) → confusing
- ❌ Throwing exceptions in callbacks → crash on resubscribe

---

## API DESIGN

**Keywords:** UniTask, IReadOnlyList, async, return type, collection, interface, callback, empty collection, file I/O

### Recommended Reading Order
1. **START HERE:** `tools/framework/.claude/docs/API_DESIGN_FULL.md` – Naming, return types, error handling

### Common Mistakes
- ❌ Returning `null` for empty collections → forces null checks
- ❌ Returning `Task<T>` instead of `UniTask<T>` → wrong ecosystem
- ❌ `UniTask<T>` with `Async` suffix → redundant naming
- ❌ Returning `T[]` instead of `IReadOnlyList<T>` → tight coupling

---

## GAMEPLAY & TIMELINE SYSTEMS

**Keywords:** timeline, dialogue, scheme, animation, sprite, audio, object, frame, transform, anchor, track, visual

### Recommended Reading Order
1. **START HERE:** [GAMEPLAY.md](GAMEPLAY.md) – Object animation, scheme structure, timeline UI patterns
2. **If editing objects:** See MEMORY: dialogue system, audio timeline, anchor points
3. **If unsure about timeline pattern:** Check `tools/framework/.claude/docs/DECISION_TREES.md` #2 (lifetime for items)

### Key Concepts
- **ObjectAnimationScheme** — Root data structure with tracks (Sprite, Audio, Dialogue, etc)
- **Timeline** — Interactive UI showing frames in sequence with drag handles
- **Dialogue** — DialogueTextTrack (character dialogue) + DialogueOptionTrack (response options)
- **ParseTracks()** — Converts scheme (serialization) → runtime objects (game data)
- **OnObjectSave()** — Persists UI edits back to scheme

### Common Mistakes
- ❌ Using wrong Lifetime for timeline entries → memory leaks
- ❌ Not calling ParseTracks() when loading → scheme data not available
- ❌ Forgetting OnObjectSave() → edits lost when reloading

---

## SPRITES, CARDS, MODIFIERS

**Keywords:** sprite catalog, LoadSpriteGroup, Sprites.Cards, SpriteGroup, IBoardCellsAnimator, ICardActionSync, card preview, modifier overlay, IModifierSource, DurationalModifierOverview, LastManStandingTurnBased, agent play, DeploymentState

### Recommended Reading Order
1. **Sprites:** `tools/framework/.claude/docs/COMMON_CONTAINER.md` — sprite groups (`RequestSpriteGroup`)
2. **Card animations / preview:** [GAMEPLAY.md](GAMEPLAY.md) — `ICardActionSync` + `IBoardCellsAnimator`
3. **Card numbers in descriptions:** [CARD_EFFECTS.md](CARD_EFFECTS.md)
4. **Modifiers / rounds / agent play:** [GAMEPLAY.md](GAMEPLAY.md)
5. **Terms:** [VOCABULARY.md](VOCABULARY.md)

### Common Mistakes
- ❌ Getter on `Sprites.X.Y` without `LoadSpriteGroup` → throw
- ❌ Animations in `CardActionSnapshotHandler` instead of `ICardActionSync` → skipped / duplicated
- ❌ Hardcoded `_Normal` card config → `_Max` uses the wrong size/duration
- ❌ `RemoveAt` + `Add` on `ViewableList` to update a field → flicker / lifetime exception

---

## TELEMETRY, MATCH METRICS, MATCH HISTORY

**Keywords:** client telemetry, startup trace, frame timing, session tag menu / match, match metrics, MatchGameMetrics, SessionMetrics, IMatchClock, MatchMs, match history, IMatch record, Grafana, /api/client-telemetry, /api/match-metrics, /api/match-history, CONSOLE_TOKEN

### Recommended Reading Order
1. **START HERE:** [TELEMETRY.md](TELEMETRY.md) — what lives on the backend, the APIs, the local and prod addresses
2. **Read the data:** skills `client-telemetry` (sessions, startups, frames) and `match-history` (matches, players, series)
3. **Collector internals:** `tools/framework/.claude/docs/MATCH_METRICS.md`, contracts linked from TELEMETRY.md

### Common Mistakes
- Reading the env file of the token yourself → the skills read it, never print or pass it
- A series name in the client that is not in `MatchGameMetrics.cs` → the console and the skill do not show it
- Looking for the telemetry of an editor session → it is off in the editor by default

---

## DECISION MAKING & ERROR LOOKUP

### Quick Decision Making
`tools/framework/.claude/docs/DECISION_TREES.md` – Which pattern to use (reactive type, lifetime, return type)

### Error Investigation
[ERRORS.md](ERRORS.md) – Error lookup table with causes and quick fixes

---

## LEARNING REFERENCES

### Self-Learning Log
[CLAUDE_MISTAKES.md](CLAUDE_MISTAKES.md) – History of mistakes and lessons learned by AI agent

### Quick Lookups
[VOCABULARY.md](VOCABULARY.md) – Consistent terminology across all docs
