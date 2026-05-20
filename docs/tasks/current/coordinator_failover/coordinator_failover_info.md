## Задача: Coordinator Failover — обработка смены координатора кластера

### Цель
Сделать backend-систему кластерной координации устойчивой к смерти и замене координатора. При падении или зависании coordinator-сервиса остальные сервисы (game, meta, silo, console) должны корректно обнаружить проблему, переключиться на новый deploy epoch и продолжить работу без ручного вмешательства.

Конкретные требования:
1. Health checks должны реально отражать состояние coordinator (не только "deployId assigned")
2. При stale heartbeat coordinator'а сервисы должны переходить в passive mode (не принимать соединения)
3. При появлении нового coordinator'а (новый deployId) все сервисы автоматически переключаются
4. Graceful shutdown — сервис при остановке должен unregister'иться из discovery
5. Все background loop'ы корректно реагируют на cancellation (быстрый shutdown)
6. Console не должен блокировать startup в headless-режиме
7. UI Console: добавить кнопку Disconnect в Discovery.razor для ручного отключения сервиса из кластера

### Контекст
Сейчас система использует deploy-epoch: каждый запуск coordinator генерирует Guid deployId, создает Orleans-grain IDeployManagement и heartbeat'ит его. Остальные сервисы получают deployId через messaging pipe и следят за ним через DeployHealthChecker.

Проблемы, найденные при анализе:
- `CoordinatorReadyHealthCheck` проверяет только `DeployId != Guid.Empty`, а не фактическую готовность
- `DeployHealthChecker.CheckHeartbeat` при stale heartbeat только логирует warning, никаких действий
- `ServiceDiscoveryStorage.Unregister` существует, но нигде не вызывается — сервисы при shutdown остаются zombie в discovery на 30 сек
- `Task.Delay` в нескольких loop'ах без cancellation token замедляет shutdown
- `DeployConstants.RequiredServices` жестко включает Console, что блокирует startup без консоли
- `TaskBalancer` при shutdown теряет in-memory задачи (нет drain logic)
- `SideEffectsWorker.StopAsync` использует `CancellationToken.None` в финальном delay — может hang

### Шаги реализации

**1. Исправить health checks для реального отражения состояния coordinator**
  1.1. `CoordinatorReadyHealthCheck` — проверять `IDeployManagement.GetState()`: `CoordinatorReady == true` И `LastHeartbeat` не stale (меньше порога). Добавить IOrleans в конструктор. — `backend/Orchestration/Extensions/CoordinatorReadyHealthCheck.cs`
  1.2. Добавить конфигурацию `CoordinatorHealthOptions` с настройкой stale threshold (по умолчанию 15s). — `backend/Cluster/Deploy/CoordinatorHealthOptions.cs` [новый файл — добавить в соответствующий .csproj]
  1.3. Зарегистрировать options в DI. — `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs`

**2. Добавить graceful shutdown с unregister из discovery**
  2.1. `ClusterParticipantStartup` — инжектировать `IHostApplicationLifetime`, подписаться на `ApplicationStopping` для вызова `_discovery.Unregister()` (через grain). — `backend/Cluster/Coordination/ClusterParticipantStartup.cs`
  2.2. `IServiceDiscovery` — добавить метод `Task Stop()` или `Task Unregister()`. — `backend/Cluster/Discovery/ServiceDiscovery.cs`
  2.3. `ServiceDiscovery` — реализовать `Unregister()`: вызвать `IServiceDiscoveryStorage.Unregister(_environment.ServiceId)`. — `backend/Cluster/Discovery/ServiceDiscovery.cs`
  2.4. `ServiceDiscovery` — в `RefreshLoop` при `OperationCanceledException` (shutdown) вызывать unregister перед выходом. — `backend/Cluster/Discovery/ServiceDiscovery.cs`

**3. Добавить passive mode при stale heartbeat и автопереключение**
  3.1. `DeployHealthChecker` — при stale heartbeat (`since > HeartbeatStaleThreshold`):
       - Если `isCoordinator == false`: вызвать `_clusterFeatures.SetAcceptingConnections(false)` и логировать critical
       - Дождаться нового deployId через pipe (существующий `CheckDeployEpoch`)
  3.2. `DeployHealthChecker` — в `CheckDeployEpoch` при смене deployId:
       - Если старый coordinator был zombie и новый deployId пришел — убедиться что `_clusterFeatures.SetAcceptingConnections(true)` вызовется после переключения (это происходит в `ClusterCoordinator`, но только для coordinator; для других сервисов нужно убедиться что they re-enable)
  3.3. `ClusterCoordinator` — добавить обработку `IDeployAware.OnDeployChanged` чтобы при смене deployId coordinator снова выполнял startup sequence (requeue stuck, enable connections). — `backend/Orchestration/Coordinator/ClusterCoordinator.cs`
  3.4. `DeployHealthChecker` — если `isCoordinator == true` и обнаружен новый deployId ( coordinator replaced ), coordinator должен вызвать `_clusterFeatures.SetAcceptingConnections(false)` и остановить принятие соединений (сейчас просто логирует "remaining passive" без действий). — `backend/Cluster/Deploy/DeployHealthChecker.cs`

**4. Улучшить heartbeat устойчивость coordinator**
  4.1. `DeployIdentity` — в `HeartbeatLoop` добавить счетчик consecutive failures. При превышении порога (например, 3) — логировать critical error и вызывать `lifetime.Terminate()` (что приведет к shutdown сервиса и его перезапуску оркестратором). — `backend/Orchestration/Coordinator/DeployIdentity.cs`
  4.2. `DeployIdentity.HeartbeatLoop` — использовать `lifetime.Token` в `Task.Delay` в catch-блоке. — `backend/Orchestration/Coordinator/DeployIdentity.cs`

**5. Исправить cancellation во всех background loop'ах**
  5.1. `ServiceDiscovery.RefreshLoop` — `Task.Delay(TimeSpan.FromSeconds(2), lifetime.Token)`. — `backend/Cluster/Discovery/ServiceDiscovery.cs`
  5.2. `TaskBalancer.CollectLoop` — `Task.Delay(options.EmptyDelayMs, lifetime.Token)` и `Task.Delay(options.NextDelayMs, lifetime.Token)`. — `backend/Infrastructure/Execution/TaskScheduling/TaskBalancer.cs`
  5.3. `TaskBalancer.ExecuteLoop` — `Task.Delay(options.EmptyDelayMs, lifetime.Token)`. — `backend/Infrastructure/Execution/TaskScheduling/TaskBalancer.cs`

**6. Сделать RequiredServices конфигурируемым**
  6.1. `ClusterParticipantStartup` — вместо `DeployConstants.RequiredServices` использовать `IClusterStartupConfig.RequiredServices`. — `backend/Cluster/Coordination/ClusterParticipantStartup.cs`
  6.2. Создать `IClusterStartupConfig` / `ClusterStartupConfig` с настраиваемым списком. — `backend/Cluster/Coordination/ClusterStartupConfig.cs` [новый файл]
  6.3. По умолчанию исключить Console из обязательных (оставить Coordinator, Meta, Game, Silo). Console добавлять только при `ServiceTag.Console`. — `backend/Cluster/Coordination/ClusterStartupConfig.cs`

**7. Добавить автоматический DeployCleanup**
  7.1. `DeployCleanup` — добавить интерфейс `IHostedService` или `BackgroundService`. — `backend/Cluster/Deploy/DeployCleanup.cs`
  7.2. Запускать cleanup при `OnDeployChanged` (в `DeployContext` или отдельном `IDeployAware`) — удалять state records от предыдущих deploy'ов. — `backend/Cluster/Deploy/DeployCleanup.cs`
  7.3. Зарегистрировать как `IHostedService`. — `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs`

**8. Тестирование**
  8.1. Написать интеграционный тест: симулировать смерть coordinator (terminate deploy lifetime), убедиться что сервисы переходят в passive mode. — `backend/Tools/Tests/Cluster/` [новый файл]
  8.2. Написать тест на graceful shutdown: убедиться что `Unregister` вызывается. — `backend/Tools/Tests/Cluster/`
  8.3. Написать тест на health check: stale heartbeat должен возвращать Unhealthy. — `backend/Tools/Tests/Cluster/`

**9. UI Console — кнопка Disconnect в Discovery**
  9.1. `Discovery.razor` — добавить инжекты `IOrleans` и `IDeployContext`. — `backend/Console/Game/Discovery/Discovery.razor`
  9.2. `Discovery.razor` — добавить кнопку `Disconnect` на карточку каждого сервиса, вызывающую `IServiceDiscoveryStorage.Unregister`. — `backend/Console/Game/Discovery/Discovery.razor`
  9.3. Добавить состояние `_disconnectingId` и индикатор загрузки при нажатии. — `backend/Console/Game/Discovery/Discovery.razor`
  9.4. **ИСПРАВЛЕНИЕ ОШИБОК в Discovery.razor:**
       - 9.4.1. `Logger.LogError` не существует в `UiComponent` — заменить на `ToastService.Error`. Добавить `[Inject] public ToastService ToastService { get; set; } = null!;`
       - 9.4.2. Скрыть кнопку Disconnect для self-сервиса (console не должен отключать сам себя). Добавить проверку `service.Id != ServiceDiscovery.Self.Id`
       - 9.4.3. Добавить `Disabled` условие при `DeployContext.DeployId == Guid.Empty` — кнопка неактивна пока deployId не получен
       - 9.4.4. После успешного unregister сделать force-refresh списка сервисов (не ждать 5-секундный loop)
       - 9.4.5. Добавить ToastService.Success при успешном отключении

**10. TaskBalancer — graceful drain при shutdown**
  10.1. `TaskBalancer` — добавить drain logic: при `lifetime.IsTerminated == true` в `ExecuteLoop` дождаться завершения всех запущенных задач (через `executionLock` / `SemaphoreSlim`) перед выходом из loop. — `backend/Infrastructure/Execution/TaskScheduling/TaskBalancer.cs`
  10.2. Альтернативно: при shutdown записать `_scheduled` задачи обратно в `ITaskQueue` (если queue поддерживает re-enqueue). — `backend/Infrastructure/Execution/TaskScheduling/TaskBalancer.cs`

**11. SideEffectsWorker — исправить hang в StopAsync**
  11.1. `SideEffectsWorker.StopAsync` — заменить `await Task.Delay(50, CancellationToken.None)` на `await Task.Delay(50, cancellationToken)`. — `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`
  11.2. `SideEffectsWorker.StopAsync` — добавить timeout: если после N итераций (например, 200 x 50ms = 10s) `_inProgress` не стал 0 — логировать warning и force-complete shutdown. — `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Cluster/Coordination/ClusterParticipantStartup.cs` | Основной startup sequence, нужен graceful shutdown |
| `backend/Cluster/Discovery/ServiceDiscovery.cs` | Discovery push/unregister, cancellation fix |
| `backend/Cluster/Deploy/DeployHealthChecker.cs` | Heartbeat monitoring, passive mode logic |
| `backend/Orchestration/Coordinator/DeployIdentity.cs` | Coordinator heartbeat, failure detection |
| `backend/Orchestration/Coordinator/ClusterCoordinator.cs` | Coordinator ready sequence, deploy change handling |
| `backend/Orchestration/Extensions/CoordinatorReadyHealthCheck.cs` | Real health check with heartbeat freshness |
| `backend/Infrastructure/Execution/TaskScheduling/TaskBalancer.cs` | Cancellation token fixes, graceful drain |
| `backend/Cluster/Deploy/DeployCleanup.cs` | Automated stale deploy cleanup |
| `backend/Cluster/Deploy/DeployConstants.cs` | RequiredServices список |
| `backend/Console/Game/Discovery/Discovery.razor` | UI для ручного отключения сервисов из кластера |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | StopAsync hang fix |

### Документация к прочтению
- `.agents/docs/COMMON_ORLEANS.md` — Grain pattern, State<T>, IOrleans, messaging
- `.agents/docs/COMMON_LIFETIMES.md` — Lifetime, Terminate, Listen
- `.agents/docs/API_DESIGN_FULL.md` — async patterns, Task.Delay with token

### Риски
- Изменение `CoordinatorReadyHealthCheck` может сломать K8s liveness probe если probe ожидает только DeployId. Нужно согласовать с инфраструктурой.
- `DeployContext.Set` при смене deployId терминирует старый lifetime — все lifetime-bound подписки (channels, pipes) должны корректно пересоздаться. Нужно проверить все `IDeployAware` реализации.
- `TaskBalancer` cancellation может повлиять на выполняющиеся задачи — drain logic должна корректно дождаться завершения или re-enqueue.
- Graceful shutdown с unregister требует чтобы Orleans client был еще жив при остановке — `IHostApplicationLifetime.ApplicationStopping` запускается до остановки hosted services, но после начала shutdown.
