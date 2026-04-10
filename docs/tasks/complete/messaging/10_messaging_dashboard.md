# 10. Messaging Health Dashboard

## Фаза: 3 (Observability) | Приоритет: P2 | Оценка: 1 день

## Проблема

Метрики есть (`BackendMetrics.Channel*`, `DurableQueue*`, `Pipe*`, `SideEffect*`), но нет единого представления здоровья messaging системы. При инциденте нужно вручную собирать данные из разных источников.

## Решение

Blazor-страница в Console с обзором всех messaging примитивов + ключевые метрики в реальном времени. Опционально: Grafana dashboard для production.

## Шаги реализации

### 1. Blazor-страница MessagingHealth

**Файл:** `backend/Orchestration/Console/Pages/Messaging/MessagingHealth.razor` (новый)

Секции:

**Side Effects Pipeline:**
- `side_effects_queue` count (SELECT COUNT)
- `side_effects_processing` count
- `side_effects_retry_queue` count
- `side_effects_dead_letter` count (после задачи 09)
- Throughput: processed/sec (из метрик)
- Error rate: failed/sec

**Runtime Channels:**
- Список активных channels (из grain directory или из клиентского `_listeners`)
- Observer count per channel
- Publish rate
- Delivery failure rate

**Runtime Pipes:**
- Список активных pipes
- Request rate
- Timeout rate
- Average latency

**Durable Queues:**
- Список активных queues
- Observer count per queue
- Push rate
- No-subscriber errors (после задачи 02)

### 2. Backend data source

**Файл:** `backend/Orchestration/Console/Pages/Messaging/MessagingHealthData.cs` (новый)

```csharp
public class MessagingHealthData {
    public MessagingHealthData(IDbSource dbSource) { ... }
    
    public async Task<SideEffectsStatus> GetSideEffectsStatus() {
        // SELECT COUNT(*) FROM side_effects_queue
        // SELECT COUNT(*) FROM side_effects_processing
        // SELECT COUNT(*) FROM side_effects_retry_queue
        // SELECT COUNT(*) FROM side_effects_dead_letter
    }
}

public record SideEffectsStatus(
    int QueueCount,
    int ProcessingCount, 
    int RetryCount,
    int DeadLetterCount);
```

### 3. Grafana dashboard (JSON model)

**Файл:** `docs/tasks/messaging/grafana_messaging_dashboard.json` (reference)

Panels:
- Side effects queue depth (time series)
- Side effects throughput (rate)
- Side effects error rate (rate)
- Channel publish rate (rate)
- Channel delivery failures (counter)
- Channel observer count (gauge)
- Pipe request rate (rate)
- Pipe timeout rate (rate)
- Pipe latency P50/P99 (histogram)
- Dead letter count (counter, alert threshold > 0)

Все метрики из `BackendMetrics.*` — уже экспортируются через OpenTelemetry.

### 4. Маршрут в Console

**Файл:** `backend/Orchestration/Console/Common/ConsoleConstants.cs`

```csharp
public const string MessagingHealth = "/messaging";
```

**Файл:** `backend/Orchestration/Console/Components/Layout/NavMenu.razor`

Добавить навигационную ссылку "Messaging".

## Ключевые файлы

| Файл | Изменение |
|------|-----------|
| `Console/Pages/Messaging/MessagingHealth.razor` | Новая страница |
| `Console/Pages/Messaging/MessagingHealthData.cs` | Data source |
| `Console/Common/ConsoleConstants.cs` | Route constant |
| `Console/Components/Layout/NavMenu.razor` | Nav link |

## Зависимости

- Задача 02 (DurableQueue no subscribers) — метрика `DurableQueueNoSubscribers`
- Задача 07 (Distributed tracing) — CorrelationId для drill-down
- Задача 09 (Dead letter) — dead_letter count

Dashboard полезен и без этих задач (базовые метрики уже есть), но полный набор — после завершения всех трёх фаз.

## Тесты

- Manual: Открыть страницу, проверить что counts соответствуют реальным
- Manual: Создать нагрузку → проверить что метрики обновляются
