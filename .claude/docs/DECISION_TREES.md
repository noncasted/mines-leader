# Decision Trees: Choose Your Pattern

Fast lookup trees for architectural decisions. Use when unsure what pattern to apply.

---

## 1. Which Reactive Type Should I Use?

```
Need to send notification/signal?
├─ Yes, but no state persistence
│  ├─ Event without params? → EventSource<void> (or EventSource)
│  └─ Event with params? → EventSource<T> or EventSource<T1, T2, ...>
│
└─ No, need to store current state
   ├─ Single value (int, string, object)?
   │  ├─ Need initial callback now? → ViewableProperty.View()
   │  ├─ Need UI sync on change? → ViewableProperty.View()
   │  └─ Need only future changes? → ViewableProperty.Advise()
   │
   └─ Collection (list, set)?
      ├─ Need dynamic iteration (add/remove)? → ViewableList<T>
      ├─ Need key→value mapping? → ViewableDictionary<K, V>
      └─ Read-only view only? → IReadOnlyList<T>
```

**Quick Reference:**
- **EventSource** → notifications, one-time signals
- **ViewableProperty** → UI bindings, current state (healthText, ammoCount)
- **ViewableList** → dynamic collections (inventory, team, enemies)

---

## 2. Which Lifetime Should I Use?

```
Subscribing to something?
├─ In MonoBehaviour.OnSetup(lifetime)?
│  └─ Use parameter lifetime directly
│     onEvent.Advise(lifetime, callback)
│
├─ In a regular class constructor?
│  ├─ Is it a service (registered in container)? → Ask for Lifetime [Inject]
│  └─ Is it not a service? → Create new Lifetime()
│
├─ Listening to an item in a collection?
│  ├─ Item has its own .Lifetime? → Use item.Lifetime
│  │  items.View(sceneLifetime, item => {
│  │      item.Events.Advise(item.Lifetime, ...) ✓
│  │  })
│  │
│  └─ Item doesn't have .Lifetime? → Create child lifetime
│     items.View(sceneLifetime, item => {
│         var itemLifetime = sceneLifetime.Child()
│         item.Events.Advise(itemLifetime, ...)
│     })
│
└─ Wrapping a callback (file dialog, async operation)?
   └─ Create scoped lifetime
      var lifetime = new Lifetime()
      return UniTask.Create(async () => { ... lifetime.Terminate() })
```

**Quick Reference:**
- **MonoBehaviour.OnSetup(lifetime)** → use parameter
- **Service class** → [Inject] Lifetime
- **Collection item** → item.Lifetime if exists, else Child()
- **One-shot operation** → new Lifetime() then Terminate()

---

## 3. How Should I Register a MonoBehaviour Service?

```
Is it a UI component or game logic service?
├─ UI component (Button, InputField, CustomWindow)?
│  ├─ Needs initialization (subscribe to events, load data)?
│  │  ✓ Implement ISceneService
│  │  ✓ Implement IScopeSetup
│  │  ✓ Add Create() and OnSetup()
│  │
│  └─ No initialization needed? → Just mark as [Inject]
│     private GameObject _someUIElement;
│
├─ Game logic service (PlayerController, ItemSpawner)?
│  └─ ✓ Implement ISceneService + IScopeSetup
│     ✓ Register in Create() with builder.RegisterComponent(this).As<IScopeSetup>()
│     ✓ Initialize in OnSetup(lifetime)
│
└─ Data-only component (no methods, just fields)?
   ├─ Needs constructor injection? → Implement IViewInjector
   │  public void Inject(IObjectResolver resolver) {
   │      resolver.Inject(this); // Auto-inject [Inject] fields
   │  }
   │
   └─ No injection needed? → Leave as-is, just [Inject] it elsewhere
```

**Quick Reference:**
- **UI + initialization** → ISceneService + IScopeSetup
- **Game logic** → ISceneService + IScopeSetup
- **Data-only component** → IViewInjector (or nothing)
- **Generic dependency** → [Inject] field elsewhere

---

## 4. What Return Type Should I Use?

```
Method does work and returns something?
├─ Async work?
│  ├─ Returns result (string, Item, IReadOnlyList)?
│  │  └─ UniTask<T>                            (NOT UniTask<T>Async)
│  │     public async UniTask<Character> LoadCharacter(string id)
│  │
│  └─ No result (just waiting)?
│     └─ UniTask
│        public async UniTask WaitForAnimation()
│
├─ Synchronous, returns a collection?
│  ├─ Can be empty? → IReadOnlyList<T>         (never null)
│  │  public IReadOnlyList<Item> GetInventory() => _items.AsReadOnly()
│  │
│  └─ Always has items? → IReadOnlyList<T>     (still safe)
│     public IReadOnlyList<Ability> GetAbilities() => _abilities.AsReadOnly()
│
└─ Synchronous, returns single object?
   ├─ Can fail/not exist? → T? (nullable)
   │  public Character? FindCharacter(string id) => _characters.TryGetValue(id, ...)
   │
   └─ Must exist? → T (non-nullable)
      public Character GetActive() => _currentCharacter
```

**Quick Reference:**
- **Async + result** → UniTask<T>
- **Async no result** → UniTask
- **Collection** → IReadOnlyList<T> (never null)
- **Single object, optional** → T?
- **Single object, required** → T

---

## 5. UI Binding: Which Method to Use?

```
Binding a value to UI text/image/etc?
├─ Show current value NOW + listen for changes?
│  └─ Use View()
│     _health.View(lifetime, hp => {
│         healthText.text = $"HP: {hp}"
│     })
│     // First callback fires immediately with current value
│
└─ Only listen for FUTURE changes?
   └─ Use Advise()
      _health.Advise(lifetime, hp => {
          healthText.text = $"HP: {hp}"
      })
      // First callback fires on next change, not current value

GOLDEN RULE: 95% of UI bindings need View(), not Advise()
```

**Quick Reference:**
- **UI display** → View() (shows current + future)
- **Event notifications** → Advise() (future only)

---

## 6. Collection Item Subscription: Lifetime Strategy

```
Listening to events from items in a collection?

items.View(sceneLifetime, item => {
    ├─ Does item have .Lifetime property? (IHasLifetime)
    │  └─ YES ✓
    │     item.Events.Advise(item.Lifetime, OnItemEvent)
    │     // Auto-cleanup when item removed from collection
    │
    └─ NO - create child lifetime
       var itemLifetime = sceneLifetime.Child()
       item.Events.Advise(itemLifetime, OnItemEvent)
       // Must cleanup manually in item.Remove() handler
})

❌ WRONG - MEMORY LEAK:
items.View(sceneLifetime, item => {
    item.Events.Advise(sceneLifetime, ...)  // Persists after item removal!
})
```

**Quick Reference:**
- **Item has .Lifetime** → use item.Lifetime
- **Item doesn't have .Lifetime** → Child() from parent lifetime

---

## 7. File I/O & Error Handling

```
Reading file (texture, config, data)?
├─ Catch all file errors?
│  └─ YES, always!
│     try {
│         var bytes = File.ReadAllBytes(path)
│         // process
│     }
│     catch {  // Catch ALL (file not found, corrupted, etc)
│         Debug.LogError($"Failed: {path}")
│         return null  // or default value
│     }
│
└─ Exception propagation?
   ├─ Critical operation (game won't work without it)?
   │  └─ throw (let caller decide)
   │
   └─ Nice-to-have operation (UI, cosmetic)?
      └─ Catch and log (app continues)
```

**Quick Reference:**
- **File I/O** → catch ALL exceptions, return null/default, don't throw
- **Critical path** → throw if truly critical
- **Optional features** → catch and log

---

## 8. When to Use NoAwait Pattern

```
Got a fire-and-forget UniTask?
├─ Is it called from MonoBehaviour?
│  └─ YES → Use NoAwait() to suppress compiler warning
│     StartCoroutineAsync().NoAwait()
│
├─ Is it called from async context?
│  └─ YES → Use await or add // warning suppression
│     await operation.NoAwait()  // or just await
│
└─ Does compiler warn about async not awaited?
   └─ YES → .NoAwait() tells compiler "intentional, no error"
```

**Quick Reference:**
- **Fire-and-forget from sync method** → .NoAwait()
- **Intentional async not awaited** → add .NoAwait()

---

## Usage: When Unsure

1. Search this file for your scenario
2. Follow the tree to narrow down
3. Use the quick reference
4. If still unsure → check COMMON_*.md for full examples

For more context:
→ [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md) - Lifetime details
→ [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md) - EventSource details
→ [COMMON_REACTIVE_VALUES.md](COMMON_REACTIVE_VALUES.md) - ViewableProperty details
→ [COMMON_REACTIVE_COLLECTIONS.md](COMMON_REACTIVE_COLLECTIONS.md) - ViewableList details
→ [API_DESIGN_FULL.md](API_DESIGN_FULL.md) - Return types & method design
