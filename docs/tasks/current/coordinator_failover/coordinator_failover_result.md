## Coordinator Failover — Результат

### Статус: Завершено

### Что сделано
1. **Health checks** — `CoordinatorReadyHealthCheck` теперь проверяет реальное состояние coordinator (ready + свежий heartbeat). Добавлен `CoordinatorHealthOptions` с настраиваемым stale threshold.
2. **Passive mode** — `DeployHealthChecker` при stale heartbeat отключает прием соединений (`SetAcceptingConnections(false)`). При смене deployId автоматически включает обратно.
3. **Graceful shutdown** — `ClusterParticipantStartup` подписывается на `ApplicationStopping` и вызывает `Unregister()`. `ServiceDiscovery.RefreshLoop` тоже вызывает `Unregister()` при cancellation.
4. **Coordinator resilience** — `DeployIdentity` считает consecutive heartbeat failures. При 3 подряд — `lifetime.Terminate()` и shutdown.
5. **Cancellation tokens** — Все background loop'ы (`ServiceDiscovery`, `TaskBalancer`) теперь передают `lifetime.Token` в `Task.Delay`.
6. **RequiredServices configurable** — `ClusterStartupConfig` позволяет настраивать обязательные сервисы. Console исключен по умолчанию.
7. **DeployCleanup as IHostedService** — `DeployCleanup` зарегистрирован как `IHostedService`.
8. **UI Disconnect** — Кнопка Disconnect в `Discovery.razor` с исправленными ошибками (ToastService, скрытие self, deployId check, force-refresh).
9. **TaskBalancer drain** — При shutdown дожидается завершения всех запущенных задач.
10. **SideEffectsWorker StopAsync fix** — Использует переданный `cancellationToken`, добавлен timeout 10s.
11. **Тесты** — 7 интеграционных тестов на failover сценарии. Все 641 тест проекта проходят.

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Orchestration/Extensions/CoordinatorReadyHealthCheck.cs` | Проверяет реальный heartbeat через IDeployManagement grain |
| `backend/Cluster/Deploy/CoordinatorHealthOptions.cs` | Новый файл — конфигурация stale threshold |
| `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs` | Регистрация CoordinatorHealthOptions и ClusterStartupConfig |
| `backend/Cluster/Discovery/ServiceDiscovery.cs` | Добавлен Unregister, cancellation token в RefreshLoop |
| `backend/Cluster/Coordination/ClusterParticipantStartup.cs` | Graceful shutdown через IHostApplicationLifetime, IClusterStartupConfig |
| `backend/Cluster/Coordination/ClusterStartupConfig.cs` | Новый файл — конфигурируемый RequiredServices |
| `backend/Cluster/Deploy/DeployHealthChecker.cs` | Passive mode при stale heartbeat, auto-switch при смене deployId |
| `backend/Orchestration/Coordinator/ClusterCoordinator.cs` | Реализует IDeployAware.OnDeployChanged для перезапуска setup |
| `backend/Orchestration/Coordinator/DeployIdentity.cs` | Consecutive failures counter, Terminate при превышении |
| `backend/Infrastructure/Execution/TaskScheduling/TaskBalancer.cs` | Cancellation tokens, graceful drain при shutdown |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | StopAsync с cancellationToken и timeout |
| `backend/Cluster/Deploy/DeployCleanup.cs` | Реализует IHostedService |
| `backend/Console/Game/Discovery/Discovery.razor` | Кнопка Disconnect с исправлениями ошибок |
| `backend/Tools/Tests/Cluster/CoordinatorFailoverTests.cs` | Новый файл — 7 интеграционных тестов |

### Отличия от плана
- TaskBalancer drain (шаг 10) реализован через ожидание `SemaphoreSlim` вместо re-enqueue в queue — это более надежный подход, так как задачи уже могут быть в процессе выполнения.

### Нерешенные вопросы
- Требуется тестирование на реальном кластере (K8s) для проверки K8s liveness probe с новым health check.
- Нужно согласовать изменение `CoordinatorReadyHealthCheck` с инфраструктурой (probe теперь возвращает Unhealthy при stale heartbeat, а не только при отсутствии deployId).
