## Coordinator Failover — Рабочие заметки

### Статус: Завершено

### Заметки

### [2026-05-20] Анализ завершен
- Полный анализ системы проведен. Ключевые проблемы:
  1. CoordinatorReadyHealthCheck не проверяет реальный heartbeat
  2. DeployHealthChecker только логирует, не действует
  3. Unregister не вызывается при shutdown
  4. Task.Delay без cancellation token
  5. Console в RequiredServices блокирует headless

### [2026-05-20] UI — кнопка Disconnect в Discovery.razor
- Добавлены инжекты IOrleans и IDeployContext
- Добавлена кнопка Disconnect на каждую карточку сервиса
- Добавлено состояние _disconnectingId с индикатором загрузки
- Метод DisconnectService вызывает IServiceDiscoveryStorage.Unregister через Orleans grain
- Пока кнопка видна на ВСЕХ сервисах (включая self). TODO: скрыть или disabled для self.

### [2026-05-20] ОБНАРУЖЕНЫ ОШИБКИ в Discovery.razor
При ревью кода выявлены критические ошибки:

1. **Компиляционная ошибка: `Logger.LogError` не существует**
   - `UiComponent` (базовый класс Discovery.razor) НЕ имеет свойства `Logger`
   - Во всех других razor-страницах консоли для ошибок используется `ToastService.Error(...)`
   - Нужно: добавить `[Inject] public ToastService ToastService { get; set; } = null!;` и заменить `Logger.LogError` на `ToastService.Error`

2. **Кнопка Disconnect видна на self-сервисе**
   - Console-сервис может случайно отключить сам себя из discovery
   - Нужно: добавить проверку `service.Id != ServiceDiscovery.Self.Id` и скрыть/disabled кнопку для текущего сервиса

3. **Кнопка активна при пустом deployId**
   - Если `DeployContext.DeployId == Guid.Empty`, вызов `Orleans.GetGrain<IServiceDiscoveryStorage>(Guid.Empty)` создаст grain с пустым ключом
   - Нужно: добавить `Disabled="@(DeployContext.DeployId == Guid.Empty || _disconnectingId == service.Id)"`

4. **Нет force-refresh после отключения**
   - После успешного unregister список обновится только через 5-секундный RefreshLoop
   - Нужно: в `finally` блоке `DisconnectService` обновить `_services` сразу после unregister

5. **Нет success-уведомления**
   - Пользователь не видит, что операция успешна
   - Нужно: добавить `ToastService.Success($"Service {serviceId} disconnected", "Done")` в try-блок

### [2026-05-20] План расширен
- Добавлен шаг 9: UI Console — кнопка Disconnect в Discovery
- Добавлен подшаг 9.4: Исправление ошибок в Discovery.razor (5 пунктов)
- Все изменения консолидированы в coordinator_failover_info.md

### [2026-05-20] Реализация шагов 1–8, 10–11

**Шаг 1 — Health checks:**
- `CoordinatorReadyHealthCheck` теперь проверяет `IDeployManagement.GetState()`: `CoordinatorReady == true` и свежесть heartbeat
- Создан `CoordinatorHealthOptions` с настройкой `StaleThreshold` (по умолчанию 15s)
- Зарегистрирован в DI через `ProjectsSetupExtensions`

**Шаг 2 — Graceful shutdown:**
- `ClusterParticipantStartup` инжектирует `IHostApplicationLifetime`, подписывается на `ApplicationStopping` для вызова `_discovery.Unregister()`
- `IServiceDiscovery` расширен методом `Unregister()`
- `ServiceDiscovery.Unregister()` вызывает `IServiceDiscoveryStorage.Unregister(_environment.ServiceId)`
- `RefreshLoop` при `OperationCanceledException` вызывает `Unregister()` перед выходом

**Шаг 3 — Passive mode:**
- `DeployHealthChecker.CheckHeartbeat` при stale heartbeat вызывает `_clusterFeatures.SetAcceptingConnections(false)` и логирует critical
- `DeployHealthChecker.CheckDeployEpoch` при смене deployId для non-coordinator вызывает `SetAcceptingConnections(true)`
- `DeployHealthChecker.CheckDeployEpoch` при смене deployId для coordinator вызывает `SetAcceptingConnections(false)`
- `ClusterCoordinator` реализует `IDeployAware.OnDeployChanged` — перезапускает startup sequence (requeue stuck, enable connections)

**Шаг 4 — Heartbeat resilience:**
- `DeployIdentity.HeartbeatLoop` добавлен счетчик consecutive failures
- При 3 подряд failures — логируется critical и вызывается `lifetime.Terminate()`
- `Task.Delay` в catch-блоке использует `lifetime.Token`

**Шаг 5 — Cancellation tokens:**
- `ServiceDiscovery.RefreshLoop` — `Task.Delay(TimeSpan.FromSeconds(2), lifetime.Token)`
- `TaskBalancer.CollectLoop` — `Task.Delay(options.EmptyDelayMs, lifetime.Token)` и `Task.Delay(options.NextDelayMs, lifetime.Token)`
- `TaskBalancer.ExecuteLoop` — `Task.Delay(options.EmptyDelayMs, lifetime.Token)`

**Шаг 6 — RequiredServices configurable:**
- Создан `IClusterStartupConfig` / `ClusterStartupConfig`
- По умолчанию: Coordinator, Meta, Game, Silo (Console исключен)
- Console добавляется только при `ServiceTag.Console`
- `ClusterParticipantStartup.WaitClusterReady` использует `_startupConfig.RequiredServices`

**Шаг 7 — DeployCleanup:**
- `DeployCleanup` реализует `IHostedService` (`StartAsync`/`StopAsync` — no-op)
- `AddDeployCleanup` регистрирует как `IHostedService`

**Шаг 8 — Тестирование:**
- `CoordinatorFailoverTests` — 7 тестов, все проходят (641 total passed)
- Проверяют: stale heartbeat -> unhealthy, fresh heartbeat -> healthy, empty deployId -> unhealthy, DeployManagement state, heartbeat update, ServiceDiscovery unregister

**Шаг 9.4 — Исправления Discovery.razor:**
- `ToastService` инжектирован, `Logger.LogError` заменен на `ToastService.Error`
- Кнопка Disconnect скрыта для self-сервиса (`service.Id != ServiceDiscovery.Self.Id`)
- Кнопка disabled при пустом deployId
- Force-refresh списка после unregister
- ToastService.Success при успешном отключении

**Шаг 10 — TaskBalancer drain:**
- При завершении `ExecuteLoop` дожидается завершения всех запущенных задач через `executionLock.WaitAsync()` перед выходом

**Шаг 11 — SideEffectsWorker StopAsync:**
- `Task.Delay(50, CancellationToken.None)` заменен на `Task.Delay(50, cancellationToken)`
- Добавлен timeout: 200 итераций x 50ms = 10s, после чего логируется warning и shutdown force-complete

### Результат
- Backend build: succeeded
- Tests: 641 passed, 0 failed
