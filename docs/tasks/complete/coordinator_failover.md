## Coordinator Failover

### Что сделано
- `CoordinatorReadyHealthCheck` проверяет реальный heartbeat coordinator, а не только наличие deployId.
- Stale heartbeat → passive mode (`SetAcceptingConnections(false)`); новый deployId включает приём обратно.
- Graceful shutdown: `Unregister()` из discovery, cancellation tokens во всех loop'ах, drain `TaskBalancer`.
- Console исключён из `RequiredServices` по умолчанию; в Discovery.razor — кнопка Disconnect.

### Ключевые файлы
- `backend/Cluster/Deploy/DeployHealthChecker.cs`
- `backend/Orchestration/Coordinator/DeployIdentity.cs`
- `backend/Cluster/Coordination/ClusterStartupConfig.cs`
- `backend/Tools/Tests/Cluster/CoordinatorFailoverTests.cs`

### Заметки
- Probe теперь Unhealthy при stale heartbeat — согласовать с K8s liveness.
- `TaskBalancer` drain ждёт `SemaphoreSlim`, а не re-enqueue.
