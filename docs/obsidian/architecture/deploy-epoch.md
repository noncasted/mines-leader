# Deploy Epoch

Каждый запуск координатора = **новый deploy-эпох**. DeployId — случайный `Guid`, сгенерированный координатором, используется как ключ ко всем эфемерным grain-стейтам кластера. Когда координатор рестартует, эпох меняется; все участники переключаются на новый grain-ключ, старые записи в БД отмирают (чистятся UI-кнопкой).

## Почему так сделано

Предыдущая реализация использовала одноразовый `CoordinatorEvents.Ready` channel + in-memory `ServiceDiscovery` beacon. Проблемы:
- Поздний подписчик пропускает `Ready` → залипает.
- Участник пропустил один beacon → считает сервис мёртвым.
- Рестарт координатора не имел протокольного отражения.

Новая модель: **единый persistent источник правды (grain) + pipe-based handshake**.

## Основные компоненты

| Компонент | Роль |
|-----------|------|
| `IDeployManagement` grain (keyed by DeployId) | Хранит `DeployId`, `LastHeartbeat`, `CoordinatorReady`. Координатор пишет heartbeat каждые 5с. |
| `IServiceDiscoveryStorage` grain (keyed by DeployId) | Хранит `Members: Dictionary<Guid, IServiceOverview>`. Один метод `Update(overview)` делает upsert, прунит stale (>30с), возвращает весь список. |
| `IClusterFeaturesGrain` (keyed by DeployId) | Персистентный state с флагами (AcceptingConnections, Matchmaking, SideEffects, SnapshotDiffGuard). |
| `DeployIdPipe` (RuntimePipe) | Координатор отвечает текущим DeployId. Участники стучатся, получают Id, только потом инициализируются. |
| `DeployIdentity` (HostedService в Coordinator) | Генерит DeployId, инициализирует grain, биндит pipe, ведёт heartbeat loop. |
| `DeployHealthChecker` (HostedService во всех сервисах) | Каждые 10с поллит pipe (детект смены epoch) + `GetState` (детект unhealthy координатора по stale heartbeat). |
| `IDeployContext` | Singleton: хранит `DeployId` + ротируемый `DeployLifetime`. При `Set(newId)` терминирует старый lifetime и нотифицирует подписчиков `IDeployAware`. |
| `IDeployAware` | Callback `OnDeployChanged(newDeployId, deployLifetime)` — подписчики (`ClusterFeatures`, `LiveState<T>`) переподключаются к новому grain-ключу и переподписываются на DeployId-scoped channels. |
| `LiveState<T>` | Замена старого `DynamicState<T>`. Канал `live-state-{type}-{deployId}` scope'ится на `DeployLifetime`. |
| `IDeployCleanup` + `/deploy` razor-страница | Удаляет записи в таблице `cluster` для всех DeployId ≠ текущего. |

## Диаграмма запуска кластера

```
┌───────────────────────┐        ┌─────────────────────────┐        ┌─────────────────────┐
│   Coordinator proc    │        │        Silo (grains)    │        │ Meta / Game / Silo  │
│                       │        │                         │        │ / Console processes │
└─────────┬─────────────┘        └───────────┬─────────────┘        └──────────┬──────────┘
          │                                   │                                  │
  [ClusterParticipantStartup]        [грейны живут здесь]                [ClusterParticipantStartup]
  Orleans → TaskBalancer                       │                                 │
  → Messaging.Start                            │                                 │
  → SetMessagingStarted ────────── flag ──┐    │                    Orleans → TaskBalancer
          │                                │   │                    → Messaging.Start
  [DeployIdentity BackgroundService]       │   │                                 │
  waits IsMessagingStarted ◄───────────────┘   │                                 │
  Guid.NewGuid() → deployId                    │                                 │
  IDeployManagement(deployId).Initialize() ────┼─► activate grain                │
  _deployContext.Set(deployId, lf)             │   writes DeployState            │
  ListenPipe(DeployIdPipe.Id)                  │                                 │
  HeartbeatLoop(5s) ──────────── grain.Heartbeat() ─► updates LastHeartbeat      │
          │                                    │                                 │
  AcquireDeployId via pipe (redundant for self, retries 0.5s) ────────► SendPipe(DeployIdPipe.Id)
  gets deployId ◄────────────────── pipe observer returns deployId ◄──┤ gets deployId
                                                                      │ _deployContext.Set
  ServiceDiscovery.Push() → grain.Update(self) ──► Members[self]     │ ServiceDiscovery.Push()
                                                                      │
  WaitClusterReady: loop discovery.Push() until Members ⊇ RequiredServices
                                                                      │ same loop
  _loop.OnLocalSetupCompleted  ──►  ClusterCoordinator.OnLocalSetup  │ _loop.OnLocalSetupCompleted
    ClusterFeatures.SetAcceptingConnections(true) ──► ClusterFeaturesGrain.Set
    ClusterFeatures.SetMatchmakingEnabled(true)
    ClusterFeatures.SetSideEffectsEnabled(true)
    grain.MarkCoordinatorReady() ──► CoordinatorReady=true
                                                                      │
  (coordinator НЕ ждёт сам себя,                  WaitCoordinatorReady: poll grain.GetState()
   skips WaitCoordinatorReady)                    until CoordinatorReady=true
                                                                      │
  _loop.OnCoordinatorSetupCompleted                 _loop.OnCoordinatorSetupCompleted
  _context.Initialize()                             _context.Initialize()
          │                                                           │
  [DeployHealthChecker (tick 10s)]                  [DeployHealthChecker (tick 10s)]
  SendPipe(DeployIdPipe.Id) → compare DeployId     SendPipe(DeployIdPipe.Id) → compare DeployId
  grain.GetState() → check LastHeartbeat stale     grain.GetState() → stale check
```

## Диаграмма рестарта координатора

```
time ┬──
     │   старый Coordinator_1 живой
     │   DeployId = OLD
     │
     │   Coordinator_1 падает (SIGTERM/crash)
     │
     │   ┌── silo: старый observer на pipe runtimepipe/deploy-id мёртв
     │   │   NEW Send-запросы идут на мёртвого observer → таймаут 10с
     │   │   (fix: [Reentrant] на RuntimePipe + DiscardObserver при fail)
     │   │
     │   │   DeployHealthChecker на Meta/Game/Silo/Console продолжает
     │   │   поллить pipe каждые 10с — получает ошибку, retry
     │
     │   Coordinator_2 стартует
     │   │
     │   │   DeployIdentity: Guid.NewGuid() = NEW
     │   │   IDeployManagement(NEW).Initialize()  ── новый grain, пустой state
     │   │   pipe.BindObserver(NEW)              ── теперь pipe отвечает NEW
     │   │
     │   │   DeployHealthChecker везде поллит pipe:
     │   │   ┌─ если OLD != NEW:
     │   │   │     non-coordinator: _deployContext.Set(NEW) →
     │   │   │       IDeployAware subscribers.OnDeployChanged:
     │   │   │         · ClusterFeatures: переподключается к ClusterFeaturesGrain(NEW)
     │   │   │           → читает state (default values), слушает channel cluster-features-{NEW}
     │   │   │         · LiveState<T>: подписывается на channel live-state-{type}-{NEW}
     │   │   │       _discovery.Push() → регистрирует self в IServiceDiscoveryStorage(NEW)
     │   │   │     coordinator: логирует "replaced", остаётся пассивным
     │   │
     │   │   ClusterCoordinator.OnLocalSetupCompleted на Coordinator_2:
     │   │     ClusterFeatures.SetAcceptingConnections/Matchmaking/SideEffects(true)
     │   │     grain.MarkCoordinatorReady() → CoordinatorReady=true в NEW deploy
     │
     │   Orleans силo через ~65с дропает OLD client (задержанный observer grace)
     │   → старые observer callbacks больше не висят
     │
     │   OLD DeployManagement/ServiceDiscoveryStorage/ClusterFeatures grain
     │   остаются в Postgres как мусор → очищаются UI-кнопкой /deploy
     ▼
```

## Что переживает что

| Сценарий | DeployId | Members | ClusterFeatures | Комментарий |
|----------|----------|---------|-----------------|-------------|
| **Silo рестарт** (coord жив) | тот же | тот же (DB) | тот же (DB) | Grain-state в Postgres, reactivate с DB. Клиенты retry-логика в loops. Прозрачно. |
| **Console рестарт** | тот же | прунится stale, перерегистрация | тот же | Через 2–30с Console снова в Members. |
| **Coordinator рестарт** | **меняется** | пустые (new grain) | **пустые** (new grain, применяются hardcoded дефолты) | Non-coord через 10с детектит, переключается. Ручные флаги теряются. |
| **Полный рестарт кластера** | меняется | пустые | пустые | Старые записи в DB для прошлых DeployId → Cleanup в Console. |

## Фиксы пайпа после первых тестов

Чтобы рестарт координатора не приводил к 50-секундным таймаутам:

1. **`[Reentrant]` на `RuntimePipe`** — новый `BindObserver` не стоит в очереди за зависшими `Send` на мёртвого observer'а.
2. **`DiscardObserverIfSame(observer)`** в catch `Send`'а — при фейле grain сам обнуляет мёртвый observer, следующие вызовы fail fast вместо очереди.
3. **`SendTimeoutSeconds` 30 → 10** — мёртвые send'ы отваливаются в 3 раза быстрее.
4. **`[Reentrant]` на `DeployManagement`** — cheap insurance для параллельных Read/Write.
5. **Coordinator skips `WaitCoordinatorReady`** — он сам только что вызвал `MarkCoordinatorReady`, ждать своего собственного write нет смысла.

## Ключевые файлы

- `backend/Cluster/Deploy/` — вся эпох-инфраструктура (grain, state, context, aware, health-checker, cleanup, live-state, pipe, constants)
- `backend/Cluster/Discovery/ServiceDiscoveryStorage*.cs` — grain членов кластера
- `backend/Cluster/Discovery/ServiceDiscovery.cs` — клиентская обёртка над grain
- `backend/Cluster/State/ClusterFeatures.cs` — DeployId-keyed state + клиент
- `backend/Cluster/Coordination/ClusterParticipantStartup.cs` — pipe-based startup flow
- `backend/Orchestration/Coordinator/DeployIdentity.cs` — генерация + heartbeat в Coordinator
- `backend/Orchestration/Coordinator/ClusterCoordinator.cs` — фиксированные дефолты + MarkCoordinatorReady
- `backend/Console/Infrastructure/Deploy/Deploy.razor` — UI Cleanup
- `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs` — pipe grain с Reentrant + DiscardObserver
