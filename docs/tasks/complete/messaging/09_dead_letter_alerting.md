# 09. Dead Letter Queue с Alerting

## Фаза: 3 (Observability) | Приоритет: P2 | Оценка: 1 день

## Проблема

Сейчас при исчерпании retry (`retryCount >= maxRetryCount`) запись просто **удаляется**:

```csharp
// SideEffectsStorage.FailProcessing()
if (retryCount >= maxRetryCount) {
    command.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
    ...
    return;
}
```

Также при ошибке десериализации создаётся `DeadLetterSideEffect`, который сразу бросает exception → тоже попадает в retry → исчерпание → удаление.

Результат: **молчаливая потеря сообщений** без каких-либо следов.

## Решение

Вместо удаления — перемещать в `side_effects_dead_letter` таблицу. Логировать с уровнем Error. Добавить метрику для alerting.

## Шаги реализации

### 1. Создать таблицу dead letter

SQL migration:
```sql
CREATE TABLE IF NOT EXISTS side_effects_dead_letter (
    id UUID PRIMARY KEY,
    payload JSONB NOT NULL,
    retry_count INT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    failed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    error_message TEXT
);
```

### 2. Изменить FailProcessing — move to dead letter вместо delete

**Файл:** `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`

```csharp
if (retryCount >= maxRetryCount) {
    await using var conn = await _dbSource.Value.OpenConnectionAsync();
    await using var tx = await conn.BeginTransactionAsync();
    
    // Move to dead letter
    await using var insertCmd = conn.CreateCommand();
    insertCmd.Transaction = tx;
    insertCmd.CommandText = @"
        INSERT INTO side_effects_dead_letter (id, payload, retry_count, created_at, failed_at, error_message)
        SELECT id, payload, retry_count, created_at, now(), @errorMessage
        FROM side_effects_processing
        WHERE id = @id
    ";
    insertCmd.Parameters.AddWithValue("id", id);
    insertCmd.Parameters.AddWithValue("errorMessage", lastError?.Message ?? "Max retries exceeded");
    await insertCmd.ExecuteNonQueryAsync();
    
    // Delete from processing
    await using var deleteCmd = conn.CreateCommand();
    deleteCmd.Transaction = tx;
    deleteCmd.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
    deleteCmd.Parameters.AddWithValue("id", id);
    await deleteCmd.ExecuteNonQueryAsync();
    
    await tx.CommitAsync();
    
    _logger.LogError("[SideEffects] Effect {Id} moved to dead letter after {RetryCount} retries", id, retryCount);
    BackendMetrics.SideEffectDeadLetter.Add(1);
    return;
}
```

### 3. Обновить сигнатуру FailProcessing

**Файл:** `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`

Добавить параметр `string? errorMessage`:
```csharp
Task FailProcessing(Guid id, int retryCount, int maxRetryCount, float incrementalRetryDelaySeconds, string? errorMessage = null);
```

### 4. Передать error message из SideEffectsWorker

**Файл:** `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`

```csharp
await _storage.FailProcessing(entry.Id,
    entry.RetryCount,
    options.MaxRetryCount,
    options.IncrementalRetryDelay,
    e.Message);
```

### 5. Метрика

В `BackendMetrics`:
```csharp
public static readonly Counter<long> SideEffectDeadLetter = ...;
```

Настроить alert: `SideEffectDeadLetter > 0` за последние 5 минут.

### 6. Blazor UI для просмотра dead letters (опционально)

Страница в Console для просмотра и ручного re-queue dead letter записей.

## Ключевые файлы

| Файл | Изменение |
|------|-----------|
| `Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | Dead letter INSERT вместо DELETE, обновлённый FailProcessing |
| `Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | Передача error message |
| SQL migration | Новая таблица side_effects_dead_letter |

## Риски

- **Dead letter accumulation**: Без очистки таблица растёт бесконечно. Добавить TTL (DELETE WHERE failed_at < now() - interval '30 days') в периодическую maintenance задачу
- **Sensitive data**: Payload может содержать пользовательские данные. Dead letter хранит полный payload — учитывать при compliance/GDPR

## Тесты

- Integration: Создать side effect который всегда fail → проверить что после max retries запись в dead_letter
- Integration: Проверить что error_message корректно сохраняется
