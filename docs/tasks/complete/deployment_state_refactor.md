## DeploymentState

### Что сделано
- `LiveState<T>` переименован в `DeploymentState<T>`: пишет в таблицу `cluster` и синхронизирует через канал `AddressableStateValue`.
- При смене deployId читает существующий стейт; если нет — берёт base value.
- `ClusterFeatures` наследуется от `DeploymentState<ClusterFeaturesState>`; grain `IClusterFeaturesGrain` удалён.

### Ключевые файлы
- `backend/Cluster/Deploy/DeploymentState.cs`
- `backend/Cluster/State/ClusterFeatures.cs`
- `backend/Cluster/Monitoring/MonitoringExtensions.cs`

### Заметки
- Канал эхоит собственные публикации. `_lastAppliedUpdate` отсекает stale echo, иначе read-modify-write откатывает более новый стейт.
- Наследники переопределяют `CreateStateIdentity` / `OnDeployChanged` / `SetValue`; логгер берётся из `ILoggerFactory` по типу наследника.
