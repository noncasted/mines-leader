# Top Mistakes — Rules Only

Full examples: → [docs/CLAUDE_MISTAKES.md](../docs/CLAUDE_MISTAKES.md)

## 1. MonoBehaviour missing ISceneService + IScopeSetup + Create()
**Result:** OnSetup() never called, silent failure.
→ Full pattern: [rules/MONOBEHAVIOUR.md](MONOBEHAVIOUR.md)

## 2. Advise(null, callback)
**Result:** Memory leak — no Lifetime = no cleanup.
Rule: EVERY Advise/View/ListenClick needs non-null lifetime.

## 3. Advise() for UI instead of View()
**Result:** UI shows nothing on load (misses initial value).
Rule: UI binding → always `View()`, never `Advise()`.

## 4. item.Events.Advise(sceneLifetime, ...) inside collection View
**Result:** Subscription leaks when item is removed.
Rule: Use `item.Lifetime` for per-item subscriptions.
→ Full: [docs/DECISION_TREES.md](../docs/DECISION_TREES.md) #6

## 5. Init in Awake() instead of OnSetup()
**Result:** Dependencies not injected yet, wrong timing.
Rule: All initialization goes in `OnSetup(IReadOnlyLifetime lifetime)`.

## 6. New .cs file not added to .csproj
**Result:** File silently not compiled, no error shown.
Fix: `grep -rl "SimilarFile" *.csproj` → find correct project → add `<Compile Include="..." />`.
