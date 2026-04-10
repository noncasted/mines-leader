# 03. Периодический RequeueStuck в SideEffectsWorker

## Фаза: 1 (Надёжность доставки) | Приоритет: P0 | Оценка: 0.5 дня

## Проблема

`SideEffectsStorage.RequeueStuck()` вызывается только один раз — при старте `SideEffectsWorker` (неявно, через `RequeueStuck` при инициализации). Если silo с worker'ом падает посреди обработки:

1. Записи остаются в `side_effects_processing` (уже вычитаны из queue)
2. Другой silo с worker'ом работает, но не знает о чужих processing записях
3. Застрявшие записи лежат вечно до ручного вмешательства или restart

Текущий `RequeueStuck()` делает `DELETE FROM side_effects_processing RETURNING ... INSERT INTO side_effects_queue` — перемещает ВСЕ processing записи обратно. Это безопасно вызывать периодически только для записей, которые "застряли" дольше определённого порога.

## Решение

Добавить периодический вызов `RequeueStuck` с фильтром по возрасту: перемещать только те записи из `side_effects_processing`, у которых `processing_started_at` старше порога (например, 5 минут).

## Шаги реализации

### 1. Добавить RequeueStuckOlderThan в ISideEffectsStorage

**Файл:** `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`

```csharp
// Interface
Task RequeueStuckOlderThan(TimeSpan age);

// Implementation
public async Task RequeueStuckOlderThan(TimeSpan age) {
    await using var connection = await _dbSource.Value.OpenConnectionAsync();
    await using var command = connection.CreateCommand();

    command.CommandText = @"
        WITH stuck AS (
            DELETE FROM side_effects_processing
            WHERE processing_started_at < @cutoff
            RETURNING id, payload, retry_count, created_at
        )
        INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
        SELECT id, payload, retry_count, created_at FROM stuck
        ON CONFLICT (id) DO NOTHING
    ";
    command.Parameters.AddWithValue("cutoff", DateTime.UtcNow - age);
    
    var moved = await command.ExecuteNonQueryAsync();
    if (moved > 0)
        _logger.LogWarning("[SideEffectsStorage] Requeued {Count} stuck entries older than {Age}", moved, age);
}
```

### 2. Добавить StuckCheckInterval в SideEffectsOptions

**Файл:** `backend/Infrastructure/Orleans/SideEffects/SideEffectsOptions.cs`

```csharp
public int StuckCheckIntervalSeconds { get; set; } = 60;
public int StuckThresholdMinutes { get; set; } = 5;
```

### 3. Добавить периодический вызов в SideEffectsWorker.Loop()

**Файл:** `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`

Добавить трекинг последней проверки:
```csharp
private DateTime _lastStuckCheck = DateTime.MinValue;
```

В `Loop()`, после `RequeueReady()`:
```csharp
var options = _config.Value;
if ((DateTime.UtcNow - _lastStuckCheck).TotalSeconds >= options.StuckCheckIntervalSeconds) {
    await _storage.RequeueStuckOlderThan(TimeSpan.FromMinutes(options.StuckThresholdMinutes));
    _lastStuckCheck = DateTime.UtcNow;
}
```

### 4. Метрика

В `BackendMetrics`:
```csharp
public static readonly Counter<long> SideEffectStuckRequeued = ...;
```

## Ключевые файлы

| Файл | Изменение |
|------|-----------|
| `Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | Новый метод `RequeueStuckOlderThan()` |
| `Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | Периодический вызов в Loop() |
| `Infrastructure/Orleans/SideEffects/SideEffectsOptions.cs` | StuckCheckIntervalSeconds, StuckThresholdMinutes |

## Риски

- **False positive**: Worker ещё обрабатывает запись (долгий side effect), а stuck check перемещает её обратно в queue → дублированное выполнение. Порог 5 минут минимизирует риск (обычный side effect < 30s). Можно добавить `FOR UPDATE SKIP LOCKED` в CTE чтобы не трогать записи, залоченные текущим worker'ом
- **Существующий RequeueStuck**: Оставить старый метод `RequeueStuck()` для вызова при startup (перемещает ВСЕ), новый `RequeueStuckOlderThan()` — для периодического вызова

## Тесты

- Integration: Записать в `side_effects_processing` вручную запись со старым `processing_started_at` → вызвать `RequeueStuckOlderThan` → проверить перемещение в queue
- Integration: Убедиться что свежие записи в processing НЕ перемещаются
