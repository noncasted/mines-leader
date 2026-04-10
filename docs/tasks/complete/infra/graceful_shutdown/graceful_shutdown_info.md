## Задача: Graceful Shutdown

### Цель
Обеспечить корректное завершение всех in-flight операций при остановке сервисов. Сейчас `SideEffectsWorker.StopAsync` пустой — in-flight side effects теряются при shutdown. `Transactions.Rollback` глотает исключения в пустом catch блоке — ошибки отката состояния невидимы. Нет настроенного shutdown timeout в Aspire.

### Контекст
- `SideEffectsWorker` имеет `_inProgress` counter (отслеживает текущие задачи), но StopAsync его не использует
- Loop запускается fire-and-forget через `.NoAwait()` — shutdown не дожидается завершения
- `MetricsSnapshotService` корректно реализует StopAsync (останавливает timer, dispose listener) — можно использовать как образец
- `ClusterParticipantStartup` использует Lifetime pattern (CancellationToken -> IReadOnlyLifetime), но не ждёт завершения текущих операций
- `ValidateOnBuild` уже включён в `ProjectsSetupExtensions.cs:144`

### Шаги реализации

**1. Реализовать StopAsync в SideEffectsWorker**
  1.1. Добавить `CancellationTokenSource _shutdownCts` для сигнализации остановки — `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`
  1.2. В `StopAsync` — отменить `_shutdownCts`, дождаться пока `_inProgress` достигнет 0 с timeout — `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`
  1.3. В Loop — проверять `_shutdownCts.Token` перед запуском новых ExecuteEntry — `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`

**2. Логировать ошибки в Transactions.Rollback**
  2.1. В catch блоке Rollback (строки 214-216) — добавить `_logger.LogError(e, "[Transactions] ...")` и инкремент метрики `TransactionRollbackFailure` — `backend/Infrastructure/Orleans/Transactions/Transactions.cs`
  2.2. Добавить метрику `TransactionRollbackFailure` — `backend/Common/Extensions/Metrics/BackendMetrics.cs`

**3. Настроить shutdown timeout в Aspire**
  3.1. Добавить `Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30))` — `backend/Orchestration/Aspire/Program.cs`

**4. Проверить ClusterParticipantStartup**
  4.1. Убедиться что `ExecuteAsync` корректно завершается при отмене — `backend/Cluster/Coordination/ClusterParticipantStartup.cs`
  4.2. При необходимости добавить ожидание текущей discovery/setup операции

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | Пустой StopAsync (строка 151), _inProgress counter (строка 41), Loop (строка 50) |
| `backend/Infrastructure/Orleans/Transactions/Transactions.cs` | Пустой catch в Rollback (строки 214-216) |
| `backend/Common/Extensions/Metrics/BackendMetrics.cs` | Добавить метрику rollback failure |
| `backend/Orchestration/Aspire/Program.cs` | Настройка shutdown timeout |
| `backend/Cluster/Coordination/ClusterParticipantStartup.cs` | BackgroundService с Lifetime pattern |
| `backend/Infrastructure/Metrics/MetricsSnapshotService.cs` | Образец корректного StopAsync |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsOptions.cs` | Options — возможно добавить ShutdownTimeout |

### Документация к прочтению
- `rules/CODE_STYLE.md` — exception handling: catch, log with [ClassName], don't rethrow

### Риски
- **Увеличение времени shutdown**: StopAsync с ожиданием _inProgress может затянуться. Нужен timeout (30 сек) с принудительным завершением.
- **Потеря side effects при timeout**: Если за 30 сек не завершились — логировать warning со списком незавершённых, дать Orleans silo корректно деактивировать grains.
