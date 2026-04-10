## Graceful Shutdown — Результат

### Статус: Завершено

### Что сделано
- SideEffectsWorker.StopAsync: добавлен _shutdownCts, polling _inProgress с timeout через cancellationToken
- Transactions.Rollback: пустой catch заменён на LogError + метрика TransactionRollbackFailure
- Aspire Program.cs: HostOptions.ShutdownTimeout = 30s
- ClusterParticipantStartup: проверен, корректно завершается через lifetime

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | _shutdownCts, StopAsync drain, проверка в Loop |
| `backend/Infrastructure/Orleans/Transactions/Transactions.cs` | LogError + метрика в Rollback catch |
| `backend/Common/Extensions/Metrics/BackendMetrics.cs` | +TransactionRollbackFailure counter |
| `backend/Orchestration/Aspire/Program.cs` | +HostOptions.ShutdownTimeout = 30s |

### Отличия от плана
- Воркер добавил stuck detection (RequeueStuckOlderThan) в Loop — полезное, но выходит за scope

### Исправления после review
- HIGH: _shutdownCts.Dispose() добавлен в StopAsync
- MEDIUM: Убран hardcoded 30s timeout, используется cancellationToken из StopAsync
- MEDIUM: Volatile.Read(ref _inProgress) вместо прямого чтения
