## Graceful Shutdown — Рабочие заметки

### Статус: Завершено

### Реализованные изменения

**1. SideEffectsWorker.StopAsync** (`backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`)
- Добавлен `CancellationTokenSource _shutdownCts`
- В `Loop`: проверка `_shutdownCts.IsCancellationRequested` перед запуском новых задач и в цикле foreach
- В `StopAsync`: отмена `_shutdownCts`, ожидание `_inProgress == 0` с timeout 30 сек, warning-лог при превышении timeout

**2. Transactions.Rollback** (`backend/Infrastructure/Orleans/Transactions/Transactions.cs`)
- Пустой `catch` заменён логированием через `_logger.LogError`
- Инкремент `BackendMetrics.TransactionRollbackFailure`
- Переменная loop изменена с `_` на `participantId` для логирования

**3. BackendMetrics** (`backend/Common/Extensions/Metrics/BackendMetrics.cs`)
- Добавлен `TransactionRollbackFailure` Counter

**4. Aspire Program.cs** (`backend/Orchestration/Aspire/Program.cs`)
- Добавлен `builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30))`

**5. ClusterParticipantStartup** (`backend/Cluster/Coordination/ClusterParticipantStartup.cs`)
- Проверка: ExecuteAsync корректно завершается при отмене через `lifetime.IsTerminated` в WaitDiscovery и `cancellation` в Task.Delay
- Дополнительных изменений не потребовалось
