# Trigger Keywords → Documentation Finder

**Read this file first when starting code review. Check for keywords below and follow the recommended reading order.**

---

## REACTIVE & STATE MANAGEMENT

**Keywords:** reactive, event, subscription, property, collection, observable, advise, view, change detection, state change, notify, listener

### Recommended Reading Order
1. **START HERE:** [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md) – EventSource, basic events
2. **If handling values:** [COMMON_REACTIVE_VALUES.md](COMMON_REACTIVE_VALUES.md) – ViewableProperty, LifetimedValue
3. **If handling collections:** [COMMON_REACTIVE_COLLECTIONS.md](COMMON_REACTIVE_COLLECTIONS.md) – ViewableList, ViewableDictionary
4. **For patterns:** [COMMON_REACTIVE_PATTERNS.md](COMMON_REACTIVE_PATTERNS.md) – Real-world usage

### Common Mistakes
- ❌ Subscribing without Lifetime → memory leak
- ❌ Using `Advise()` when you need `View()` → missing initial state
- ❌ Mixing EventSource with ViewableProperty → wrong pattern for values

---

## LIFETIME & RESOURCE MANAGEMENT

**Keywords:** lifetime, resource cleanup, termination, IReadOnlyLifetime, scope, disposal, parent-child, token

### Recommended Reading Order
1. **START HERE:** [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md) – Lifetime basics, creation patterns
2. **For patterns:** [COMMON_LIFETIMES_PATTERNS.md](COMMON_LIFETIMES_PATTERNS.md) – Advanced scenarios

### Common Mistakes
- ❌ Lifetime never terminates → memory leak
- ❌ Ignoring lifetime in callbacks → untracked subscriptions
- ❌ Not using parent-child hierarchy → manual cleanup needed

---

## DEPENDENCY INJECTION & SERVICES

**Keywords:** MonoBehaviour, ISceneService, IScopeSetup, Create, OnSetup, [Inject], VContainer, registration, DI, service, scope builder

### Recommended Reading Order
1. **START HERE:** [COMMON_CONTAINER.md](COMMON_CONTAINER.md) – DI fundamentals, MonoBehaviour registration

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
1. **START HERE:** [CODE_STYLE.md](CODE_STYLE.md) – Organization, naming, structure

### Common Mistakes
- ❌ Wrong member order (fields before constructor) → harder to read
- ❌ Abbreviated names (`_obs` instead of `_observers`) → confusing
- ❌ Throwing exceptions in callbacks → crash on resubscribe

---

## API DESIGN

**Keywords:** UniTask, IReadOnlyList, async, return type, collection, interface, callback, empty collection, file I/O

### Recommended Reading Order
1. **START HERE:** [API_DESIGN.md](API_DESIGN.md) – Naming, return types, error handling

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
3. **If unsure about timeline pattern:** Check [DECISION_TREES.md](DECISION_TREES.md) #2 (lifetime for items)

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

## DECISION MAKING & ERROR LOOKUP

### Quick Decision Making
[DECISION_TREES.md](DECISION_TREES.md) – Which pattern to use (reactive type, lifetime, return type)

### Error Investigation
[ERRORS.md](ERRORS.md) – Error lookup table with causes and quick fixes

---

## LEARNING REFERENCES

### Self-Learning Log
[CLAUDE_MISTAKES.md](CLAUDE_MISTAKES.md) – History of mistakes and lessons learned by AI agent

### Quick Lookups
[VOCABULARY.md](VOCABULARY.md) – Consistent terminology across all docs
