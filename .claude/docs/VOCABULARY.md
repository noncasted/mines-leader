# Vocabulary Index

**Consistent terminology across all documentation. When in doubt, use terms from this list.**

## Core Concepts

| Concept | Primary Term | Aliases | File |
|---------|--------------|---------|------|
| Managed lifetime | Lifetime | IReadOnlyLifetime, scope lifetime, resource scope | COMMON_LIFETIMES.md |
| Lifetime termination | Terminate | End, dispose, cleanup | COMMON_LIFETIMES.md |
| Parent-child relationship | Lifetime hierarchy | Scope hierarchy, nesting | COMMON_LIFETIMES.md |
| Subscription cleanup | Lifetime-scoped subscription | Automatic cleanup, auto-unsubscribe | COMMON_LIFETIMES.md |
| Fire event | Invoke | Emit, send, signal | COMMON_REACTIVE_BASICS.md |
| Observable event | EventSource | Event stream, event signal | COMMON_REACTIVE_BASICS.md |
| Value + lifetime | LifetimedValue | State container, reactive value | COMMON_REACTIVE_VALUES.md |
| Named value property | ViewableProperty | Named state, reactive property | COMMON_REACTIVE_VALUES.md |
| Observable collection | ViewableList | Reactive list, observed collection | COMMON_REACTIVE_COLLECTIONS.md |
| Observable dictionary | ViewableDictionary | Reactive map, observed key-value store | COMMON_REACTIVE_COLLECTIONS.md |
| Future-only notification | Advise | Subscribe to future events only | COMMON_REACTIVE_*.md |
| Immediate + future notification | View | Subscribe with initial state callback | COMMON_REACTIVE_*.md |
| Dependency injection | VContainer | DI container, scope builder | COMMON_CONTAINER.md |
| MonoBehaviour registration | ISceneService | Component service, MB registration | COMMON_CONTAINER.md |
| Initialization callback | OnSetup | Setup phase, lifecycle hook | COMMON_CONTAINER.md |
| Scope initialization | IScopeSetup | Setup interface, lifecycle interface | COMMON_CONTAINER.md |
| Dependency receiver | [Inject] attribute | Constructor injection, dependency marker | COMMON_CONTAINER.md |
| Code organization | Member order | Class member structure, declaration order | CODE_STYLE.md |
| Local helper function | Local function | Inner function, method-scoped function | CODE_STYLE.md |

## MonoBehaviour Services

| Pattern | Term | Description |
|---------|------|-------------|
| Marker interface | `ISceneService` | Tags component for VContainer registration |
| Lifecycle interface | `IScopeSetup` | Component wants `OnSetup(IReadOnlyLifetime)` callback |
| Injection interface | `IViewInjector` | Component receives field injection via Inject(resolver) |
| Registration method | `Create(IScopeBuilder builder)` | Component registers itself with scope |
| Initialization method | `OnSetup(IReadOnlyLifetime lifetime)` | Called when scope initializes |

## Timeline & UI Systems

| Concept | Term | Description |
|---------|------|-------------|
| Frame sequence UI | Timeline | Interactive editor showing frames in horizontal layout |
| Timeline UI component | TimelineEntry | Single frame display with resize handles |
| Timeline resize handle | Drag handle | Left/right control for changing frame duration |
| Timeline container | ResponsiveContainer | Auto-arranges entries in timeline |
| Audio visualization | AudioTimelineVolumes | Shows waveform (amplitude bars) for audio track |
| Object transform editor | LevelObjectVisualTransform | Position + Scale editor for visual objects |
| Frame boundary | AudioBoundaryEntry | Start/End offset control for audio playback window |

## Dialogue System

| Concept | Term | Description |
|---------|------|-------------|
| Dialogue UI root | DialogueView | Main container, loads all dialogue elements |
| Character text display | DialogueTextView | TMP_Text with transform editor for dialogue lines |
| Response button | DialogueOptionView | Button + text for player response options |
| Dialogue text track | DialogueTextTrack | Scheme track containing character dialogue frames |
| Dialogue option track | DialogueOptionTrack | Scheme track containing player response options |
| Frame color system | dialogue_color_system.md | Rich text tags for per-word coloring |
| Character anchor point | ObjectAnchorPoint | Position marker for dialogue attachments |

## Object Animation & Schemes

| Concept | Term | Description |
|---------|------|-------------|
| Animation data root | ObjectAnimationScheme | Root config containing all tracks (sprite, audio, dialogue) |
| Scheme track | Track (Sprite/Audio/Dialogue) | Collection of frames for one animation type |
| Animation frame | Frame | Single unit of animation with duration + data |
| Scheme layer | Serialization layer | ObjectAnimationScheme and track types (stored in config) |
| Runtime layer | Runtime data | Converted from scheme (via ParseTracks) for game use |
| Scheme conversion | ParseTracks | Extension method converting scheme → runtime objects |
| Scheme persistence | OnObjectSave | Saves UI edits back to ObjectAnimationScheme |
| Sprite animation | ObjectAnimationSpriteFrame | Frame with sprite, duration, and anchor points |
| Audio playback | ObjectAnimationAudioTrack | Track with clip, StartOffset, EndOffset |

## Rules (DO NOT USE SYNONYMS)

- ❌ Do NOT mix: "Lifetime" + "Token" (token is part of lifetime, not synonym)
- ❌ Do NOT mix: "Advise" + "View" (completely different behaviors)
- ❌ Do NOT mix: "EventSource" + "ViewableProperty" (different use cases)
- ❌ Do NOT mix: "Setup" + "Create" (different phases, Setup is later)
- ❌ Do NOT mix: "Scheme" + "Runtime" (different layers, converted via ParseTracks)
- ❌ Do NOT mix: "Timeline" + "Track" (timeline is UI, track is data structure)
- ✅ Do use: One term consistently throughout your response
