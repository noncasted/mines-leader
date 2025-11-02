# Aspire

Modern .NET application orchestration. Manages cluster startup, service dependencies, database initialization, and observability (OpenTelemetry, health checks, service discovery).

## Architecture

```
+--------------------+
| AppHost            |  // Orchestration entrypoint
| (DistributedApp)   |
+----------+---------+
           |
    +------+-------+-------+-------+-------+
    |      |       |       |       |       |
    v      v       v       v       v       v
[Startup] [Silo] [Gateway] [Coordinator] [Game] [Console]
   |        ^
   |        |
   +-----WaitForCompletion()
           |
  +-WaitFor(silo)--+
  |                |
[Backend]      [Coordinator]
                  [Game]
                  [Console]
```

## Cluster Services

All services are added as projects and managed by AppHost:

```csharp
// Database initialization
var startup = builder.AddProject<Startup>("startup");

// Orleans cluster host
var silo = builder.AddProject<Silo>("silo");

// Service entry points
var coordinator = builder.AddProject<Coordinator>("coordinator");
var backend = builder.AddProject<BackendGateway>("backend");
var game = builder.AddProject<GameGateway>("game");
var console = builder.AddProject<Console>("console");
```

## Startup Order

```
startup → silo → [coordinator, backend, game, console]
```

**Dependency Graph:**

```csharp
silo.WaitForCompletion(startup);      // Silo waits for DB init
coordinator.WaitFor(silo);             // Coordinator waits for Orleans
backend.WaitFor(silo);                 // Gateway waits for Orleans
game.WaitFor(silo);                    // Game waits for Orleans
console.WaitFor(silo);                 // Console waits for Orleans
```

**Key Pattern:**
- **WaitForCompletion()** - Service must complete successfully before next starts
- **WaitFor()** - Service waits until target is running

## Startup Service - Database Initialization

Runs before Orleans cluster starts. Executes on first cluster run only.

### SQL Scripts (First Run)

```
1. PostgreSQL-Main.sql
   - Main tables, extensions, functions

2. PostgreSQL-Persistence.sql
   - Orleans state persistence tables

3. PostgreSQL-Clustering.sql
   - Orleans cluster membership tables

4. PostgreSQL-Clustering-3.7.0.sql
   - Orleans 3.7.0 compatibility schema
```

Then creates state tables for each grain type from `States.StateTables`.

### Subsequent Runs

Only clears Orleans membership table:
```sql
TRUNCATE TABLE orleansmembershiptable;
```

### Connection Retry

```csharp
// Retries 10 times with 1 second delay
// Ensures database is ready before initialization
var connection = await GetConnection();  // Waits for DB availability
```

## ServiceDefaults - Configuration

All services call `AddServiceDefaults()` in their setup:

```csharp
builder.AddServiceDefaults();
```

### What It Provides

**OpenTelemetry Integration:**
- Logging with structured fields
- Metrics collection (ASP.NET Core, HTTP Client, Runtime)
- Distributed tracing with TraceId/SpanId
- Exports to OTLP endpoint (Jaeger, custom)

**Health Checks:**
- `/health` - Full health check
- `/alive` - Liveness check (for Kubernetes)
- Tags: "live" for liveness probes

**Service Discovery:**
- Automatic service location
- Works with Orleans service discovery
- HttpClient configured with resilience

**HTTP Client Resilience:**
- Retry policies
- Timeout handling
- Circuit breaker patterns

### OpenTelemetry Configuration

```csharp
builder.ConfigureOpenTelemetry()
{
    // Logging: Include formatted messages and scopes
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;

    // Metrics: ASP.NET Core, HTTP Client, Runtime, Orleans
    metrics.AddMeter("Microsoft.Orleans")
    metrics.AddMeter("Backend")

    // Tracing: All sources + ASP.NET Core + HTTP Client
    tracing.AddSource(source.Name)
    tracing.AddAspNetCoreInstrumentation(...)
    tracing.AddHttpClientInstrumentation()
}
```

## Environment Configuration

### Via Environment Variables

```bash
# Database connection
DB_CONNECTION_STRING=postgresql://user:pass@localhost:5432/mines

# Service identification
SERVICE_NAME=backend-gateway

# OpenTelemetry export
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317

# Aspire Dashboard
ASPIRE_TOKEN=your-dashboard-token

# Game server location
GAME_SERVER_URL=http://localhost:5000
```

### Via Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
    "db": "postgresql://localhost:5432/mines",
    "OpenTelemetry": "http://localhost:4317"
  }
}
```

## Startup Sequence

```
1. AppHost.Run()
   ↓
2. Resolve environment variables
   ↓
3. SetupDB() - Set connection string for all services
   ↓
4. Start Startup service
   ↓
5. Startup initializes PostgreSQL
   ↓
6. Startup stops (WaitForCompletion)
   ↓
7. Start Silo (Orleans cluster)
   ↓
8. Silo loads from PostgreSQL state
   ↓
9. Start dependent services
   ↓
10. Services ready for requests
```

## Health Checks

### Development Mode

```http
GET /health
  ↓
Returns: {"status": "Healthy"}
```

```http
GET /alive
  ↓
Checks: "live" tag only
Returns: {"status": "Healthy"}
```

### Production Mode

- `/health` and `/alive` disabled by default
- Enable via configuration if needed

## Observability Stack

### Local Development Ports

| Component | Port | Dashboard |
|-----------|------|-----------|
| Aspire | 15236 | Cluster orchestration |
| Jaeger | 16686 | Trace visualization |
| Kibana | 5601 | Log visualization |
| OpenTelemetry | 4317 | OTLP endpoint |

### Trace Correlation

```
Every request gets:
- TraceId (globally unique)
- SpanId (per operation)
- Parent SpanId (operation chain)
```

Example log with correlation:
```
[2024-01-15 10:32:45.123Z] [User] [Projection]
TraceId: 0af7651916cd43dd8448eb211c80319c
SpanId: b7ad6b7169203331
Message: Sending cached SharedUser to {Id} [UserId]
```

## Registration Pattern

Each service Setup method follows same pattern:

```csharp
// In Backend/Gateway/Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();              // Add observability
builder.SetupBackendGateway();             // Add domain services
var app = builder.Build();
app.MapDefaultEndpoints();                 // Health checks
```

## Critical Rules

1. **Always call AddServiceDefaults()** - Required for observability
2. **WaitForCompletion() for startup** - Silo depends on DB
3. **WaitFor() for dependent services** - Prevents race conditions
4. **Connection string propagation** - SetupDB() applies to all projects
5. **Health check endpoints** - Development-only by default
6. **OpenTelemetry export** - Automatic correlation of requests across services

## Logging Tags (Aspire System)

| Tag | Component | Usage |
|-----|-----------|-------|
| `[Startup]` | ProjectStartup | Database initialization |
| `[Silo]` | Orleans | Cluster startup |
| `[Coordinator]` | Coordinator | Cluster coordination |
| `[User]` [EntryPoint]` | UserConnectionEntryPoint | Client connection lifecycle |

## Key Files

| File | Purpose |
|------|---------|
| `Aspire/AppHost/Program.cs` | Orchestration entrypoint, service dependencies, startup order |
| `Aspire/ServiceDefaults/AspireExtensions.cs` | OpenTelemetry, health checks, service discovery |
| `Aspire/Startup/Program.cs` | Startup service bootstrap |
| `Aspire/Startup/ProjectStartup.cs` | Database initialization logic |
| `Aspire/Startup/PostgreSQL-*.sql` | Database schema scripts |

## Integration Points

- **Orleans** - Silo waits for DB initialization
- **PostgreSQL** - Persists Orleans state and cluster membership
- **OpenTelemetry** - Aggregates logs, metrics, traces
- **Service Discovery** - All services register automatically
- **Health Checks** - Kubernetes readiness/liveness probes
