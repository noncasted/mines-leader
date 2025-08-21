# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Structure

This is a "mines-ultra" project consisting of multiple interconnected components:

- **backend/**: .NET 9 microservices architecture using Microsoft Orleans and Aspire
  - **Aspire/**: Application orchestration and service defaults
  - **Backend/**: Core business logic services (Gateway, Users, Matches)
  - **Game/**: Game-specific logic and session management
  - **Infrastructure/**: Cross-cutting concerns (Orleans, messaging, storage)
  - **Management/**: Configuration and web management console
  - **Common/**: Shared utilities and extensions

- **client/**: Unity C# game client
  - Uses dependency injection with VContainer
  - Includes shared assemblies that mirror backend domain models
  - Multiple assembly definitions for different game areas (GamePlay, Menu, etc.)

- **shared/**: Cross-platform C# libraries shared between backend and client
  - **Backend/**: Backend-specific shared code
  - **Domain/**: Core domain models
  - **Game/**: Game logic shared between client and server
  - **Protocol/**: Communication protocols
  - **Session/**: Session management

- **tools/**: Development tools and utilities

## Build Commands

### Backend (.NET)
```bash
# Build entire backend solution
dotnet build backend/backend.sln

# Run with Aspire (development orchestration)
dotnet run --project backend/Aspire/AppHost

# Build specific project
dotnet build backend/Path/To/Project.csproj
```

### Client (Unity)
The Unity client uses Visual Studio solution files and should be opened in Unity Editor or built using Unity's build pipeline. The client includes both editor and player variants of assemblies.

### Shared Libraries
```bash
# Build shared libraries
dotnet build shared/Domain/Domain.csproj
dotnet build shared/Game/Game.csproj
# etc.
```

## Architecture Overview

**Backend Architecture:**
- **Orleans-based actor system**: Uses Microsoft Orleans grains for distributed state management
- **Microservices**: Separated by domain (Users, Matches, Game sessions)
- **Event-driven**: Uses Orleans streams and messaging infrastructure
- **Aspire orchestration**: Modern .NET application orchestration for development
- **PostgreSQL**: Primary data store with Orleans persistence

**Client-Server Communication:**
- Shared domain models between client and backend through the `shared/` libraries
- Protocol definitions in `shared/Protocol/`
- Real-time communication for game sessions

**Key Patterns:**
- **Grain interfaces and implementations**: Orleans actors define business logic boundaries
- **Dependency injection**: Used throughout both backend (.NET DI) and client (VContainer)
- **Assembly separation**: Clear boundaries between different functional areas
- **Shared code**: Domain models and protocols shared between client and server

## Development Guidelines

**Backend Development:**
- All projects target .NET 9
- Use Orleans grains for stateful services
- Follow the established project structure in the solution
- Leverage Aspire for local development orchestration

**Client Development:**
- Use VContainer for dependency injection
- Follow Unity's assembly definition patterns
- Maintain compatibility with shared libraries

**Shared Code:**
- Keep domain models clean and framework-agnostic
- Protocol definitions should be versioned carefully
- Shared code must work in both Unity and .NET environments

## Client Internal System Documentation

**⚠️ IMPORTANT**: When working with client-side **dependency injection**, **service registration**, **lifetime management**, **scope building**, **entity builders**, **scene services**, or any VContainer-related patterns, you MUST first read:
- `docs/claude/DEPENDENCY_INJECTION.md` - Complete guide to the custom DI system built on VContainer
- `docs/claude/LIFETIMES.md` - Resource management and automatic cleanup patterns

These documents contain critical information about:
- **Service Registration Patterns**: ServiceCollection, ScopeBuilder, EntityBuilder usage
- **Lifetime Management**: Automatic cleanup, memory leak prevention, async cancellation
- **Scene Services**: ISceneService implementation patterns and best practices
- **Class Organization**: Strict member ordering and coding conventions
- **VContainer Integration**: Proper usage of the underlying DI framework

**Trigger Keywords**: If you encounter or need to work with any of these concepts, consult the documentation first:
- ServiceCollection, ScopeBuilder, EntityBuilder
- ISceneService, IServiceCollection, IBuilder
- Lifetime, ILifetime, IReadOnlyLifetime
- Service registration, dependency injection patterns
- VContainer, LifetimeScope, scope loading
- Asset integration, hierarchical scopes
- Unity component injection, scene loading

**⚠️ IMPORTANT**: When working with **documentation**, **logging patterns**, **tag systems**, **Orleans attributes**, or **code organization standards**, you MUST first read:
- `docs/claude/DOCUMENTATION_PATTERNS.md` - Complete guide to documentation conventions and tag systems

This document contains critical information about:
- **Logging Tag System**: `[Domain] [Component]` structured logging patterns
- **Orleans State Attributes**: Custom attribute classes and state management patterns
- **Documentation Standards**: How to document systems with consistent tag patterns
- **Code Organization**: Alignment between tags, namespaces, and file structure
- **Best Practices**: DO's and DON'Ts for tag usage and documentation writing

**Documentation Keywords**: If you encounter or need to work with any of these concepts, consult the documentation first:
- Logging tags, domain tags, component tags
- `[User]`, `[Match]`, `[Game]`, `[Config]`, `[Messaging]` domain patterns
- `[Projection]`, `[Entity]`, `[Deck]`, `[Progression]` component patterns
- Orleans state attributes, TransactionalStateAttribute patterns
- `[States.UserProjection]`, `[States.UserEntity]` and similar state management
- Documentation writing, tag documentation, logging documentation
- Code organization standards, namespace alignment

**⚠️ IMPORTANT**: When working with **Meta Backend Projection**, **reactive state synchronization**, **backend projections**, or **client-server data flow**, you MUST first read:
- `docs/claude/META_BACKEND_PROJECTION.md` - Complete guide to the client-side backend projection system

This document contains critical information about:
- **Backend Projection System**: Reactive state synchronization between Orleans actors and Unity client
- **IBackendProjection Interface**: Type-safe reactive property pattern for backend state
- **BackendProjectionHub**: Central dispatcher and type routing for projection updates
- **Lifetime Management**: Automatic cleanup and memory management for projections
- **Network Protocol**: SharedBackendProjection wrapper and union type registration
- **Usage Patterns**: Subscription, async waiting, and service registration patterns

**Backend Projection Keywords**: If you encounter or need to work with any of these concepts, consult the documentation first:
- BackendProjection, IBackendProjection, BackendProjectionHub
- SharedBackendProjection, SharedBackendUser, SharedMatchmaking
- Reactive projections, state synchronization, backend updates
- `[Meta] [Projection]` logging patterns and projection routing
- Client-server data flow, Orleans projection updates
- LifetimedValue, ViewableProperty, reactive state management
- Projection registration, RegisterBackendProjection, MetaServicesExtensions

## Documentation Management

**⚠️ IMPORTANT**: When the user asks you to **update docs**, **write docs**, **create documentation**, or **document** any system/feature, you MUST:
~~~~~~~~~~~~
1. **Create/update the documentation file** in `docs/claude/` directory
2. **Update this CLAUDE.md file** to add:
   - New trigger keywords related to the documented topic
   - Direct reference to the new/updated documentation file
   - Brief description of what the documentation covers

This ensures future Claude instances will automatically reference the documentation when working with those concepts.

**Documentation Workflow:**
- User requests documentation → Create/update file in `docs/claude/`
- Add trigger keywords and file reference to this CLAUDE.md
- Ensure documentation follows established patterns and is comprehensive
- Include code examples, best practices, and common pitfalls