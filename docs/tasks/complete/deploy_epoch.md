## Deploy epoch

### Что сделано
- Каждый запуск координатора генерит случайный `DeployId` (Guid), который становится ключом для трёх persistent grain'ов: `IDeployManagement` (heartbeat + CoordinatorReady), `IServiceDiscoveryStorage` (Members с автопрунингом stale), `IClusterFeaturesGrain` (фича-флаги).
- Участники получают DeployId через `RuntimePipe("deploy-id")` с retry; координатор раздаёт через `DeployIdentity` hosted service.
- `IDeployContext` хранит текущий DeployId + ротируемый `DeployLifetime`. `IDeployAware.OnDeployChanged` нотифицирует подписчиков (`ClusterFeatures`, `LiveState<T>`) при смене эпох.
- `DeployHealthChecker` каждые 10с поллит pipe (детект смены epoch → переключение) и `LastHeartbeat` (детект unhealthy координатора).
- `DynamicState<T>` удалён, заменён на `LiveState<T>` c DeployId-scoped channel и lifetime.
- Razor-страница `/deploy` с кнопкой Cleanup удаляет записи прошлых DeployId.
- Фиксы пайпа: `[Reentrant]` на `RuntimePipe` + `DeployManagement`, обнуление мёртвого observer'а при фейле Send, `SendTimeoutSeconds` 30→10. Координатор пропускает `WaitCoordinatorReady` для самого себя.

### Ключевые файлы
- `backend/Cluster/Deploy/` — grain'ы, context, aware, health-checker, cleanup, live-state, pipe, constants
- `backend/Cluster/Discovery/ServiceDiscoveryStorage*.cs` — grain членов + `ServiceDiscovery` как тонкий клиент над ним
- `backend/Cluster/State/ClusterFeatures.cs` — grain + IDeployAware-клиент
- `backend/Orchestration/Coordinator/DeployIdentity.cs` — генерация DeployId, pipe handler, heartbeat
- `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs` — Reentrant + DiscardObserver
- `backend/Console/Infrastructure/Deploy/Deploy.razor` — Cleanup UI
- `docs/obsidian/architecture/deploy-epoch.md` — диаграммы flow + рестарта

### Заметки
- Рестарт координатора теряет runtime-флаги `ClusterFeatures` (пустой grain нового DeployId). Хардкод-дефолты в `ClusterCoordinator.OnLocalSetupCompleted` восстанавливают базовые флаги, но ручные toggle через Console теряются.
- Orleans держит disconnected client ~65с (grace). До этого silo может накапливать observer-колбэки на мёртвого клиента — отсюда важность Reentrant + DiscardObserver на pipe grain'е.
- Cleanup старых DeployId-записей — только по кнопке в Console (`/deploy`). Автоматики нет специально.
- `DeployCleanup` не может использовать `Common.StatesLookup` напрямую (генерится только в сборках, где подключены `Generators`). Вместо этого — через `IStateStorage.Registry.Get<T>()`.
