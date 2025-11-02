# Infrastructure

Module `Infrastructure/` contains cross-domain infrastructure for the project. It is the foundation on which all business services are built.

## Module Structure

```
Infrastructure/
  Coordination/       - Cluster startup coordination
  Discovery/          - Service discovery
  Loop/               - Service lifecycle
  Messaging/          - Message queues and pipes
  Orleans/            - Orleans-specific logic
  StorableActions/    - Batchers and ClusterState
  TaskScheduling/     - Task scheduler
```

## Module Relationships

```
+------------------------------------------------------------------+
|                         COORDINATION                              |
|  ClusterParticipantStartup -> ServiceLoop -> ILocalSetupCompleted |
|                          |                                        |
|                          v                                        |
|                  CoordinatorEvents (ready)                        |
+------------------------------------------------------------------+
                              |
                              | uses
                              v
+---------------+    +---------------+    +-----------------------+
|   DISCOVERY   |<-->|   MESSAGING   |<-->|   STORABLE ACTIONS    |
|               |    |               |    |                       |
| ServiceTag    |    | Queue (1:N)   |    | BatchWriter           |
| ServiceOverview    | Pipe (1:1)    |    | ClusterState          |
+---------------+    +---------------+    +-----------------------+
         |                   |                        |
         +-------------------+------------------------+
                             |
                             v
                    +-----------------+
                    |     ORLEANS     |
                    |  Grains, Storage|
                    |  Transactions   |
                    +-----------------+
```

## Service Startup Order

Service startup (non-Coordinator) is managed via `ClusterParticipantStartup`:

1. **Orleans started** - wait for Orleans (silo/client) to start
2. **TaskBalancer** - start task scheduler
3. **Messaging** - start queues and pipes
4. **ServiceDiscovery** - start service discovery
5. **WaitDiscovery** - wait for all required services (Coordinator, Gateway, Game, Silo)
6. **ILocalSetupCompleted** - execute all local initializations
7. **Wait CoordinatorEvents.Ready** - wait for coordinator readiness
8. **ICoordinatorSetupCompleted** - execute post-coordinator initializations

## Coordinator

Coordinator is a special service that:
- Does not wait for itself via discovery
- Wakes up BatchWriters via `BatchWritersWakeUp`
- Manages `ClusterFeatures.AcceptingConnections`
- Sends `CoordinatorEvents.Ready` when cluster is ready

## Key Interfaces

### Lifecycle
- `ILocalSetupCompleted` - called after local startup
- `ICoordinatorSetupCompleted` - called after coordinator is ready

### Infrastructure Services
- `IMessaging` - send/receive messages
- `IServiceDiscovery` - information about available services
- `ITaskScheduler` - task scheduling
- `IClusterState<T>` - distributed cluster state

## Key Files

| File | Purpose |
|------|---------|
| `Coordination/Startup/ClusterParticipantStartup.cs` | Service startup logic |
| `Coordination/Coordinator/ClusterCoordinator.cs` | Coordinator logic |
| `Loop/ServiceLoop/Stages.cs` | Startup stage interfaces |
| `Discovery/Services/ServiceDiscovery.cs` | Service discovery |
| `Messaging/Service/Messaging.cs` | Messaging entry point |

## Detailed Documentation

- [INFRASTRUCTURE_DISCOVERY.md](INFRASTRUCTURE_DISCOVERY.md) - Service Discovery
- [INFRASTRUCTURE_MESSAGING.md](INFRASTRUCTURE_MESSAGING.md) - Queues and Pipes
- [INFRASTRUCTURE_STORABLEACTIONS.md](INFRASTRUCTURE_STORABLEACTIONS.md) - BatchWriter and ClusterState
