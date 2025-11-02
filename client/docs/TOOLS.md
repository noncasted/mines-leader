# Client Tools Module

Development utilities, editor tools, and code generation infrastructure.

## Module Structure

```
Tools/
├── EditorTools/         - Editor-only development utilities
├── MemoryPackTools/     - Serialization code generation
├── SceneTools/          - Scene testing and mocking
├── Settings/            - Project configuration
└── Linker configuration - IL2CPP build optimization
```

---

## Editor Tools (EditorTools/)

**Assembly:** `Tools.Editor.asmdef` (Editor-only)

### Assembly Management System

**Purpose:** Maintain clean dependency graph and prevent circular references.

**Components:**

**DomainConstructor.cs:**
- Scans all `.asmdef` files in project
- Parses C# source files for namespaces and `using` statements
- Builds dependency graph for all assemblies
- Validates cyclic dependencies and throws exceptions
- Discovers assemblies in multiple locations:
  - `/Features/GamePlay`, `/Features/Loop`, `/Features/Menu`
  - `/Global`, `/Plugins`
  - `Library/PackageCache` (Unity packages)
  - `Packages` (local packages)

**Interfaces:**

```csharp
IAssembly              // Core assembly contract
├─ IAssemblyPath      // Path information
├─ IAssemblyDetails   // Metadata (namespaces, usings)
├─ IAssemblyDefines   // Platform includes/excludes
└─ IAssemblyToggles   // Compiler flags

IAssemblyDomain       // Domain-level categorization
```

**Implementations:**

```csharp
Assembly              // Full assembly representation
AssemblyPath          // Path resolution
AssemblyDetails       // Namespace/dependency metadata
AssemblyToggles       // Compiler configuration
AssemblyDefines       // Platform and version definitions
RawAssembly           // Intermediate representation
AssemblyFile          // Direct .asmdef deserialization
VersionDefinesObject  // Version-specific defines
```

### Assembly Validation and Rewriting

**AssemblyReferencesValidator.cs**

Menu: `Tools/Assemblies/Invalidate asmdef references`

Features:
- Removes unnecessary assembly references
- Checks if referenced namespaces are actually used
- Validates implicitly required references
- Skips Unity framework and external libraries
- Writes cleaned assembly definitions back

**RewriteAssemblies.cs**

Menu: `Tools/Assemblies/Rewrite`

Features:
- Bulk operation on owned assemblies
- Disables `autoReferenced` flag
- Ensures consistent assembly configuration

**ResetAssembliesEngineReferences.cs**

- Handles engine reference management

### Runtime Editor Tools

**LinkerGenerator.cs**

Menu: `Tools/Generate link.xml`

Interface: `IPreprocessBuildWithReport` (runs before player builds)

Purpose: Generate `link.xml` configuration for IL2CPP code stripping

Behavior:
```csharp
1. Collect all project assemblies
2. Exclude Editor, Test, Demo assemblies
3. Create link.xml preserve list
4. Output to `/Tools/Settings/link.xml`
5. Used by IL2CPP to prevent code stripping
```

**Preserved Assemblies (39 total):**
- Core: Common, GamePlay, Global, Internal, Loop, Menu, Meta, Startup
- MemoryPack: MemoryPack.Core, MemoryPack.Generator
- DI: VContainer
- Async: UniTask (and Addressables, DOTween, TextMeshPro, Linq variants)
- UI: MPUIKit, NaughtyAttributes, Sirenix (Odin Inspector)
- Utilities: UniClipboard
- Third-party: ADF-RBG.Mulligan, projectCloner, BuildReportTool, etc.
- Shared: Shared library

**ScriptableObjectCreator.cs**

Menu: `Assets/Create Scriptable Object` (priority -1000)

Features:
- Hierarchical menu tree of all ScriptableObject types
- Live preview of created object
- Search toolbar for finding types
- Type filtering (excludes abstract classes)
- Custom menu path support via `CreateAssetMenuAttribute`
- Automatic unique asset naming
- OdinInspector-based UI

**ScriptableObjectsDestroyer.cs**

Menu: `Assets/Destroy Nested Objects`

Features:
- Batch deletion utility for selected ScriptableObjects
- Immediate asset database refresh

**MockSwitcher.cs**

Automatic initialization: `[InitializeOnLoad]`

Features:
- Listens for play mode state changes
- Finds first `MockBase` component in scene
- Triggers mock processing when entering play mode
- Enables scene-based game testing without full setup

---

## MemoryPack Tools (MemoryPackTools/)

**Assembly:** `Tools.MemoryPack.asmdef` (Runtime)

**Purpose:** Centralize network protocol serialization registration.

### UnionInitializer.cs

Inherits from `EnvPreprocessor` (environment preprocessing system)

**Registers Union Types:**

**Entity Payloads:**
```csharp
MenuPlayerPayload
CardCreatePayload
PlayerCreatePayload
```

**Event Payloads:**
```csharp
MenuChatMessagePayload
```

**Network Contexts:**
```csharp
EmptyResponse
SharedBackendUser (all types)
SharedBackendMatch (all types)
SharedMatchmaking (all types)
SharedSession (all types)
SharedGameAction (all types)
SharedGameEvent (all types)
```

**Process:**

```csharp
UnionBuilder<IEntityPayload>
    .Add<MenuPlayerPayload>()
    .Add<CardCreatePayload>()
    // ... more types
    .Build();  // Generates union serializers

UnionBuilder<IEventPayload>
    // ... event types
    .Build();

UnionBuilder<INetworkContext>
    // ... network contexts
    .Build();
```

**Purpose:** Enable efficient binary serialization/deserialization for client-server communication.

---

## Scene Tools (SceneTools/)

**Assembly:** `Tools.Scene.asmdef` (Runtime)

**Purpose:** Game scene testing without full multiplayer setup.

### MockBase.cs

Abstract base class for scene mocking:

Features:
- `[DisallowMultipleComponent]` - Only one per scene
- DI scope management via `ILoadedScope`
- Bootstrap procedure:
  ```csharp
  1. Load InternalScopeConfig
  2. Create InternalScopeLoader
  3. Load global scope from internal
  4. Load meta scope from global
  5. Resolve and process mock implementation
  ```
- Automatic cleanup on application quit
- Async UniTask-based processing

### GameMock.cs

Extends `MockBase` for game session testing:

Supports game modes:
- `GameMode.Single` - Single player testing
- `GameMode.PvP` - Multiplayer testing

Processing flow:
```csharp
1. Bootstrap DI scopes
2. Initialize matchmaking or opponent search
3. Execute mock processor:
   ├─ ProcessSingleMock() for single player
   └─ ProcessPvPMock() for PvP
4. Run game with mock systems
5. Display results
```

**Usage:**
- Add GameMock component to test scene
- Set game mode and other options
- Enter play mode
- MockSwitcher auto-initializes
- Game runs with mock backend

### MenuMock.cs

Extends `MockBase` for menu UI testing:

Processing flow:
```csharp
1. Bootstrap DI scopes
2. Load menu mock scene
3. Resolve IMenuLoop
4. Start menu processing
5. Test menu flows without backend
```

**Usage:**
- Add MenuMock component to test scene
- Enter play mode
- MockSwitcher auto-initializes
- Menu runs with mock backend

---

## Settings Module (Settings/)

### Rendering & Graphics Configuration

**DefaultVolumeProfile.asset**
- URP volume settings
- Post-processing configuration

**Renderer2D.asset**
- 2D renderer configuration
- Sprite rendering optimization

**UniversalRenderPipelineGlobalSettings.asset**
- URP global settings
- Renderer pipeline configuration

**UniversalRP.asset**
- Universal render pipeline asset
- Quality and performance settings

### Asset Storage

**AssetStorage.asset**
- Asset storage configuration
- Updated Nov 26 21:17
- Links all asset categories

### Options Configuration (Build Variants)

**Options_Release.asset**
- Main release configuration
- Production settings

**Options_Release_Debug.asset**
- Release debug variant
- Debug symbols included

**Options_Release_Version.asset**
- Version information
- Build metadata

**Options_Release_Assets.asset**
- Asset configuration
- Asset bundling settings

**Options_Release_Backend.asset**
- Backend connectivity
- API endpoints

**Purpose:** Environment-specific build configurations for different deployment scenarios.

### Linker Configuration

**link.xml**

IL2CPP code stripping preservation list (39 assemblies)

Prevents removal of essential code:
- Client assemblies
- Serialization libraries
- DI frameworks
- Async utilities
- Third-party libraries

Usage:
```csharp
LinkerGenerator.Generate() → link.xml
  ↓
IL2CPP build process
  ↓
Preserves specified assemblies
  ↓
Final build includes full functionality
```

---

## Key Patterns and Workflows

### Assembly Management Workflow

```
1. DomainConstructor scans .asmdef files
2. Parses C# source for namespaces/using
3. Builds dependency graph
4. Validates for cycles
5. Tools can:
   ├─ AssemblyReferencesValidator: Remove unnecessary references
   ├─ RewriteAssemblies: Fix configurations
   └─ Output: Clean assembly definitions
```

### Mock Testing Pattern

```
Scene with MockBase subclass
  ↓ (Enter play mode)
MockSwitcher detects mock
  ↓
Bootstrap creates DI hierarchy
  └─ Internal scope
  └─ Global scope
  └─ Meta scope (with mocks)
  └─ Game/Menu scope
  ↓
Mock processor executes
  ├─ Game logic or Menu flows
  └─ Mock backend responds
  ↓
Auto-cleanup on quit/stop
```

### Serialization Registration

```
UnionInitializer executes (preprocessor)
  ↓
UnionBuilder collects types
  ├─ IEntityPayload
  ├─ IEventPayload
  └─ INetworkContext
  ↓
MemoryPack generates serializers
  ↓
Types ready for client-server communication
```

### Build Optimization

```
BeforeBuild: LinkerGenerator.Generate()
  ├─ Creates link.xml
  └─ Specifies assemblies to preserve
  ↓
IL2CPP compiles with link.xml
  ├─ Removes unused code safely
  └─ Keeps specified assemblies intact
  ↓
Final build: Smaller size, full functionality
```

---

## Integration Points

**Assembly Management:**
- Used: All assemblies in project
- Prevents: Circular dependencies, missing references
- Called by: Manual menu items or CI pipeline

**Mock Testing:**
- Used by: Developers during development
- Integrates with: Scene-based testing
- Requires: GameMock or MenuMock component

**Serialization:**
- Used by: Network communication
- Integrates with: Client-server messages
- Called by: UnityEngine (build time)

**Linker Configuration:**
- Used by: IL2CPP build process
- Generates: link.xml file
- Called by: Build pipeline (preprocessor)

---

## Key Files

| File | Purpose |
|------|---------|
| `EditorTools/Assemblies/DomainConstructor.cs` | Assembly scanning |
| `EditorTools/Assemblies/Actions/AssemblyReferencesValidator.cs` | Reference validation |
| `EditorTools/LinkerGenerator.cs` | Link.xml generation |
| `EditorTools/ScriptableObjectCreator.cs` | ScriptableObject creation UI |
| `EditorTools/MockSwitcher.cs` | Mock auto-detection |
| `MemoryPackTools/UnionInitializer.cs` | Serialization registration |
| `SceneTools/MockBase.cs` | Mock base implementation |
| `SceneTools/GameMock.cs` | Game testing |
| `SceneTools/MenuMock.cs` | Menu testing |

---

## Assembly Dependencies

**EditorTools.asmdef:**
- Sirenix OdinInspector (UI)
- Standard Unity editor APIs

**MemoryPackTools.asmdef:**
- MemoryPack (serialization)
- GamePlay, Menu, Meta (types)
- Common.Network (protocols)

**SceneTools.asmdef:**
- Internal (scope loading)
- Global (services)
- Meta (backend mock)
- GamePlay, Menu (systems)
- VContainer, UniTask

---

## See Also

- `OVERVIEW.md` - Project architecture
- `COMMON.md` - Network protocols
- `GAMEPLAY.md` - Game logic
- `MENU.md` - Menu system
