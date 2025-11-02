# Infrastructure: Discovery

Service discovery module. Allows services to find each other in the cluster.

## How It Works

Each service periodically (every 10 seconds) publishes its `ServiceOverview` to a shared queue `service-discovery`. All services listen to this queue and update their local list of available services.

```
Service A --+                  +-- Service A (local entries)
Service B --+-> Queue ---------+-- Service B (local entries)
Service C --+  "service-discovery" +-- Service C (local entries)
```

## Key Types

### ServiceTag

Service type in the cluster:

```csharp
public enum ServiceTag {
    Coordinator = 100,
    Gateway = 200,
    Game = 300,
    Silo = 400,
    Console = 500,
}
```

### IServiceOverview

Basic service information:

```csharp
public interface IServiceOverview {
    Guid Id { get; }           // Unique instance ID
    ServiceTag Tag { get; }    // Service type
    DateTime UpdateTime { get; } // Last update time
}
```

### GameServerOverview

Extended information for Game server (includes URL):

```csharp
public class GameServerOverview : IServiceOverview {
    // ... base fields
    public required string Url { get; init; }  // URL for client connections
}
```

## API

### IServiceDiscovery

```csharp
public interface IServiceDiscovery {
    IServiceOverview Self { get; }                    // Current service info
    IReadOnlyDictionary<Guid, IServiceOverview> Entries { get; }  // All known services
    Task Start(IReadOnlyLifetime lifetime);           // Start discovery
}
```

### Extension Methods

```csharp
// Get random Game server
var server = serviceDiscovery.RandomServer();
```

## Rules

1. **TTL = 30 seconds** - service is removed from list if not updated for 30 seconds
2. **Update interval = 10 seconds** - heartbeat publish frequency
3. **Game URL** - taken from environment variable `GAME_SERVER_URL`, default `http://localhost:5268`

## Registration

```csharp
builder.AddEnvironment(ServiceTag.Gateway);  // Register service type
builder.AddServiceDiscovery();               // Register discovery
```

## Logging

| Tag | Component | Description |
|-----|-----------|-------------|
| `[Discovery]` | ServiceDiscovery | All discovery logs |

## Key Files

| File | Purpose |
|------|---------|
| `Discovery/Environment/ServiceTag.cs` | Service types enum |
| `Discovery/Environment/ServiceEnvironment.cs` | Current service context |
| `Discovery/Environment/IServiceOverview.cs` | Service DTOs |
| `Discovery/Services/ServiceDiscovery.cs` | Main logic |

## Typical Scenarios

### Wait for All Services at Startup

```csharp
// ClusterParticipantStartup.cs
var requiredServices = new[] {
    ServiceTag.Coordinator,
    ServiceTag.Gateway,
    ServiceTag.Game,
    ServiceTag.Silo,
};

while (AllServicesFound() == false)
    await Task.Delay(TimeSpan.FromSeconds(1));
```

### Get Game Server URL

```csharp
var gameServer = serviceDiscovery.RandomServer();
var wsUrl = gameServer.Url.ServerUrlToWebSocket();
```
