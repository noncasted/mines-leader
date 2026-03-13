# Code Examples Reference

**Complete working examples for all documentation topics.**

All examples are in: `client/Assets/Docs/Claude/`

## Lifetimes Examples
File: [Docs_Lifetimes.cs](../../client/Assets/Docs/Claude/Docs_Lifetimes.cs)

| Example | Line | Topic |
|---------|------|-------|
| Standalone | #L16 | Creating standalone lifetime |
| Child | #L31 | Creating child lifetime |
| Hierarchy | #L49 | Lifetime hierarchy |
| Intersect | #L81 | Lifetime intersection |
| From Token | #L100 | Creating from CancellationToken |
| Scoped Subscription | #L118 | Using lifetime for cleanup |
| Resource Cleanup | #L135 | Cleanup patterns |
| Conditional Lifetime | #L150 | Conditional patterns |
| Check IsTerminated | #L169 | Checking state |
| Rule: Listen on terminated | #L251 | Behavior on terminated |
| Rule: Auto-terminates | #L265 | Child auto-termination |
| Rule: Terminate twice safe | #L278 | Idempotency |
| Rule: Listeners cleared | #L291 | Cleanup guarantees |
| Rule: Recursive termination | #L306 | Hierarchy cleanup |
| Special: TerminatedLifetime | #L326 | Pre-terminated singleton |
| Special: Lazy Token | #L343 | Lazy initialization |
| Debugging: Memory leak | #L362 | Finding leaks |
| Debugging: Single operation | #L384 | Canceling operations |
| Debugging: Long-lived | #L407 | Long-lived lifetimes |
| Debugging: Ungraceful | #L438 | Shutdown patterns |

## Reactive Examples
File: [Docs_Reactive.cs](../../client/Assets/Docs/Claude/Docs_Reactive.cs)

| Example | Line | Topic |
|---------|------|-------|
| EventSource Basic | #L24 | Simple event |
| EventSource Params | #L39 | Event with parameters |
| ViewableDelegate | #L39 | Named event wrapper |
| LifetimedValue | #L54 | Value + lifetime |
| ViewableProperty | #L75 | Named value property |
| ViewableList | #L106 | Observable list |
| ViewableDictionary | #L141 | Observable dictionary |
| Advise vs View | #L171 | Subscription patterns |
| Pattern: UI Binding | #L203 | Binding to UI |
| Pattern: Item-scoped | #L220 | Item subscriptions |
| Pattern: Conditional | #L241 | Conditional logic |
| Pattern: Async Coord | #L268 | Async coordination |
| Rule 1: All Advise | #L289 | Lifetime requirement |
| Rule 2: View immediate | #L310 | View behavior |
| Rule 3: Item lifetime | #L331 | Item lifetime |
| Rule 4: Value lifetime | #L353 | Value lifetime |
| Rule 5: Dispose clears | #L374 | Cleanup on dispose |
| Special: ModifiableList | #L259 | Iteration-safe list |

## API Design Examples
File: [Docs_ApiDesign.cs](../../client/Assets/Docs/Claude/Docs_ApiDesign.cs)

| Example | Line | Topic |
|---------|------|-------|
| UniTask Async | #L24 | Async method naming |
| IReadOnlyList | #L39 | Return type pattern |
| Empty Collections | #L57 | Empty vs null |
| File Sprite Loading | #L87 | File I/O pattern |
| FileBrowser Integration | #L129 | Callback wrapping |
| Callback to UniTask | #L158 | Pattern: wrap callbacks |
| Load with Timeout | #L179 | Pattern: timeout |
| UniTask Rule | #L205 | Naming rule |
| IReadOnlyList Rule | #L211 | Collection rule |
| Empty Rule | #L218 | Empty result rule |
| File Errors Rule | #L224 | Error handling |
| UnityObject Rule | #L239 | Full name requirement |
| Callback Rule | #L246 | Callback wrapping rule |

## Code Style Examples
File: [Docs_CodeStyle.cs](../../client/Assets/Docs/Claude/Docs_CodeStyle.cs)

| Example | Line | Topic |
|---------|------|-------|
| Member Organization | #L33 | Member order |
| Field Naming | #L69 | Field naming convention |
| Method Logic | #L93 | Method structure |
| Local Functions | #L134 | Local function patterns |
| Exception Handling | #L156 | Safe subscribe pattern |
| Logging | #L181 | Logging format |
| NoAwait | #L200 | NoAwait usage |
| Collection Init | #L209 | Inline initialization |
| Property Init | #L217 | required + init |
| Iteration Mod | #L240 | Modification during iteration |
| TryGetValue | #L258 | Dictionary lookup |
| GC.KeepAlive | #L278 | Keep alive usage |
| Braces | #L295 | Brace placement |
| Service Template | #L308 | Complete template |

## Container/DI Examples
File: [Docs_Container.cs](../../client/Assets/Docs/Claude/Docs_Container.cs)

| Example | Line | Topic |
|---------|------|-------|
| Scope Setup Flow | #L24 | Complete lifecycle (10 phases) |
| ISceneService | #L112 | MonoBehaviour registration |
| MonoBehaviour Service | #L129 | Full service pattern |
| Scope Builder | #L211 | Builder usage |
| View Injector | #L164 | Runtime injection |
| Async Lifecycle | #L228 | Async setup/cleanup |
| Dependency + Lifetime | #L248 | Injected dependency lifetime |
| Event Loop Phases | #L270 | Execution order |
| Rule: Register | #L293 | Registration requirement |
| Rule: Lifetime valid | #L312 | Lifetime validity |
| Rule: Order guaranteed | #L331 | Initialization order |
| Rule: Use IViewInjector | #L360 | Injection pattern |
| Rule: AddViewEvents | #L386 | Lifecycle connection |

---

## How to Use This Reference

1. **Find your topic** in the table above
2. **Click the line number** to jump to example in code
3. **Read complete context** - all surrounding code explains the pattern
4. **Run if needed** - examples are compilable C# code

## Important Notes

- All `.cs` files are in `client/Assets/Docs/Claude/`
- Line numbers point to the start of each example
- Examples include comments explaining the "why"
- Related examples are grouped together
- Use with corresponding `.md` files in `docs/` for full explanation

## Quick Links

- **Lifetimes:** [Docs_Lifetimes.cs](../../client/Assets/Docs/Claude/Docs_Lifetimes.cs) + [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md)
- **Reactive:** [Docs_Reactive.cs](../../client/Assets/Docs/Claude/Docs_Reactive.cs) + [COMMON_REACTIVE_*.md](COMMON_REACTIVE_BASICS.md)
- **API Design:** [Docs_ApiDesign.cs](../../client/Assets/Docs/Claude/Docs_ApiDesign.cs) + [API_DESIGN.md](API_DESIGN.md)
- **Code Style:** [Docs_CodeStyle.cs](../../client/Assets/Docs/Claude/Docs_CodeStyle.cs) + [CODE_STYLE.md](CODE_STYLE.md)
- **Container/DI:** [Docs_Container.cs](../../client/Assets/Docs/Claude/Docs_Container.cs) + [COMMON_CONTAINER.md](COMMON_CONTAINER.md)
