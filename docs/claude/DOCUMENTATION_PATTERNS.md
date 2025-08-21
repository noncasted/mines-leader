# DOCUMENTATION_PATTERNS.md

This document describes the specific documentation patterns and conventions used throughout this codebase, particularly the tag system found in logging and attributes.

## Tag System Overview

The codebase uses a structured tag system with square brackets `[Tag]` to provide contextual information in various scenarios. These tags serve as semantic markers for different purposes.

## Logging Tags

### Pattern: `[Domain] [Component] Message`

**Structure:**
- **Domain Tag**: Identifies the business domain or system area
- **Component Tag**: Specifies the specific component or operation
- **Message**: Descriptive text with structured placeholders

**Examples from UserProjection:**
```csharp
_logger.LogWarning("[User] [Projection] Failed to force notify. User {Id} is not connected", this.GetPrimaryKey());
_logger.LogInformation("[User] [Projection] Sending cached {Type} to {Id}", payload.GetType().Name, this.GetPrimaryKey());
_logger.LogInformation("[User] [Projection] Saving cached {Type} to {Id}", payload.GetType().Name, this.GetPrimaryKey());
_logger.LogWarning("[User] [Projection] Failed to send cached. User {Id} is not connected", this.GetPrimaryKey());
_logger.LogInformation("[User] [Projection] Sending one time {Type} to {Id}", payload.GetType().Name, this.GetPrimaryKey());
_logger.LogWarning("[User] [Projection] Failed to send one time. User {Id} is not connected", this.GetPrimaryKey());
```

**Startup Logging Examples:**
```csharp
_logger.LogInformation("[Startup] [User] [Projection] Starting user projection entry point");
_logger.LogInformation("[Startup] [Config] [Settings] Loading application configuration");
_logger.LogInformation("[Startup] [Game] [Session] Initializing game session manager");
_logger.LogInformation("[Startup] [Messaging] [Queue] Connecting to message broker");
```

### Common Domain Tags
- **`[User]`** - User-related operations and state management
- **`[Match]`** - Game match logic and processing
- **`[Game]`** - Core game mechanics and session management
- **`[Config]`** - Configuration and settings management
- **`[Messaging]`** - Inter-service communication and message passing

### Common Component Tags
- **`[Projection]`** - Data projection and real-time updates
- **`[Entity]`** - Core entity state management
- **`[Deck]`** - Deck management and card operations
- **`[Progression]`** - User progression and achievements
- **`[Gateway]`** - API gateway and entry points
- **`[Queue]`** - Message queue operations
- **`[Session]`** - Session lifecycle management
- **`[Startup]`** - Application and service initialization

## Attribute Tags

### Orleans State Attributes

**Pattern: Custom attribute classes extending Orleans state management**

```csharp
// State constant definition
public const string User_Projection = "User_Projection";

// Attribute class extending Orleans
public class UserProjectionAttribute() : TransactionalStateAttribute(User_Projection, User_Projection);

// Usage in dependency injection
[States.UserProjection] ITransactionalState<UserProjectionState> state
```

**Key Examples:**
- `[States.UserProjection]` - User projection state management
- `[States.UserEntity]` - Core user entity state
- `[States.UserProgression]` - User progression tracking
- `[States.UserDeck]` - User deck state
- `[States.Match]` - Match state management
- `[States.Config]` - Configuration persistence

### Orleans Concurrency Attributes

**Transaction Control:**
```csharp
[AlwaysInterleave]
[Transaction(TransactionOption.Create)]
Task ForceNotify();

[AlwaysInterleave]
[Transaction(TransactionOption.Join)]
Task SendCached(IProjectionPayload payload);
```

**Serialization Attributes:**
```csharp
[Alias(States.User_Projection)]
[GenerateSerializer]
public class UserProjectionState
{
    [Id(0)]
    public Dictionary<string, IProjectionPayload> Values { get; } = new();
}
```

## Documentation Writing Guidelines

### 1. Tag Documentation Format

When documenting a system that uses tags, follow this structure:

```markdown
## [Domain] [Component] System

**Purpose**: Brief description of what this system does

**Tag Pattern**: `[Domain] [Component]` - Explanation of when this tag appears

**Key Operations**:
- Operation name - Brief description with tag context
- Another operation - Brief description with tag context

**Example Usage**:
```csharp
// Code example showing tag usage
```

**Related Tags**: List other tags that commonly appear with this one
```

### 2. State Management Documentation

For Orleans state management systems:

```markdown
## State: [StateName]

**Attribute**: `[States.StateName]` - Usage context
**Type**: `StateType` - Data structure description
**Persistence**: Transactional/Persistent - Storage pattern
**Access Pattern**: Description of how state is accessed and modified

**Operations**:
- Read operations and their logging tags
- Write operations and their logging tags
- Transaction boundaries and their tags
```

### 3. Logging Documentation

For systems with extensive logging:

```markdown
## Logging Patterns

**Standard Format**: `[Domain] [Component] {Message}`

**Log Levels**:
- **Information**: Normal operations and state changes
- **Warning**: Recoverable errors and connection issues
- **Error**: System failures and unrecoverable states

**Common Messages**:
- "Sending {Type} to {Id}" - Data transmission operations
- "Failed to {Operation}. {Reason}" - Error conditions
- "{Entity} {Action}" - State change notifications

**Startup Patterns**:
- "[Startup] [Domain] [Component] Starting {ServiceName}" - Service initialization
- "[Startup] [Domain] [Component] Loading {ConfigType}" - Configuration loading
- "[Startup] [Domain] [Component] Connecting to {ExternalService}" - External dependencies
- "[Startup] [Domain] [Component] Initializing {SystemComponent}" - Component setup
```

### 4. Cross-System Tag Relationships

Document how tags relate across different systems:

```markdown
## Tag Relationships

**[User] System Tags**:
- `[User] [Entity]` - Core user state operations
- `[User] [Projection]` - Real-time user updates
- `[User] [Progression]` - Achievement and progress tracking
- `[User] [Deck]` - Card deck management

**Message Flow**: Show how operations flow between tagged systems
**State Dependencies**: Document which state attributes depend on others
**Error Propagation**: How errors cascade through tagged operations
```

## Best Practices

### ✅ DO's

• **Consistent Tag Hierarchy**: Always use `[Domain] [Component]` format
• **Meaningful Domain Names**: Use clear, business-relevant domain tags
• **Specific Component Names**: Component tags should clearly identify the operation area
• **Structured Messages**: Use consistent parameter naming in log messages (`{Id}`, `{Type}`, etc.)
• **Tag Documentation**: Document all tag patterns when creating new systems

### ❌ DON'Ts

• **Mixed Tag Formats**: Don't mix `[Tag]` with other bracket styles
• **Generic Tags**: Avoid vague tags like `[System]` or `[Process]`
• **Inconsistent Casing**: Maintain consistent capitalization in tags
• **Untagged Critical Operations**: Important operations should always have domain/component tags
• **Orphaned Tags**: Don't create tags without documenting their purpose and usage

## Code Organization with Tags

### File Structure Reflection
Tags should reflect the actual code organization:

```
backend/Backend/Users/Projections/     → [User] [Projection]
backend/Backend/Users/Entities/        → [User] [Entity]
backend/Backend/Users/Progression/     → [User] [Progression]
backend/Backend/Matches/Entities/      → [Match] [Entity]
backend/Game/Session/                   → [Game] [Session]
```

### Namespace Alignment
Ensure tags align with namespace structure for easier navigation and debugging.

### Testing Tags
In test environments, consider prefixing tags with `[Test]` or using mock-specific tags to differentiate test logs from production logs.