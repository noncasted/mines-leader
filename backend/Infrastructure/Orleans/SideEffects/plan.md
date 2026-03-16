# Plan: Side Effects System Implementation

## Overview

Система отложенного выполнения задач с гарантиями at-least-once и retry-логикой.
Два типа эффектов: простые (`ISideEffect`) и транзакционные (`ITransactionalSideEffect`).
Транзакционные используют ту же `NpgsqlTransaction`, что и коммит `Transactions.Run()`.

## Architecture

```
Grain.Method()
  └─ Transactions.Run(action)
       ├─ action() вызывает grain.RegisterSideEffect(effect)
       │    └─ GrainTransactionHandler.RecordSideEffect()
       ├─ CollectResult() → собирает States + SideEffects
       ├─ pgTransaction.BeginTransaction()
       │    ├─ stateStorage.Write(pgTx, states)        ← уже работает
       │    ├─ sideEffectsStorage.Write(pgTx, effects) ← INSERT в side_effects_queue
       │    ├─ context.OnSuccessCallbacks(pgTx)        ← NEW: для TransactionalSideEffect
       │    └─ pgTransaction.Commit()
       └─ OnSuccess() на всех участниках

SideEffectsWorker (background, ICoordinatorSetupCompleted)
  └─ Loop():
       ├─ storage.RequeueReady()            ← retry_queue → queue (если retry_after <= now)
       ├─ Read(freeSlots) → move queue → processing (атомарно через CTE)
       ├─ foreach entry: ExecuteEntry() [параллельно, в пределах ConcurrentExecutions]
       └─ Task.Delay(ScanDelay)

ExecuteEntry(entry):
  ├─ если ITransactionalSideEffect:
  │    await transactions.Run(async () => {
  │        context.OnSuccessCallbacks.Add(tx => storage.CompleteProcessing(tx, id));
  │        await effect.Execute(orleans);
  │    });
  └─ иначе (простой):
       await effect.Execute(orleans);
       await storage.CompleteProcessing(id);

  на ошибке: storage.FailProcessing(id, retryCount) → side_effects_retry_queue
             если retryCount >= MaxRetryCount → удалить без retry
```

## DB Tables

```sql
-- Очередь на выполнение
CREATE TABLE side_effects_queue (
    id uuid NOT NULL PRIMARY KEY,
    payload jsonb NOT NULL,
    retry_count integer NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL
);
CREATE INDEX ix_side_effects_queue ON side_effects_queue (created_at);

-- В процессе выполнения (захвачены воркером)
CREATE TABLE side_effects_processing (
    id uuid NOT NULL PRIMARY KEY,
    payload jsonb NOT NULL,
    retry_count integer NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL,
    processing_started_at timestamptz NOT NULL
);

-- Ожидают retry (backoff)
CREATE TABLE side_effects_retry_queue (
    id uuid NOT NULL PRIMARY KEY,
    payload jsonb NOT NULL,
    retry_count integer NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL,
    retry_after timestamptz NOT NULL
);
CREATE INDEX ix_side_effects_retry_queue ON side_effects_retry_queue (retry_after);
```

## Payload format (JSON в колонке payload)

```json
{"t": "Namespace.ConcreteEffectClass", "d": "{...serialized effect json...}"}
```

`d` — JSON-строка конкретного типа, вложенная как строка (позволяет десериализовать в нужный тип).

---

## Step 1 — Fix: GrainTransactionHandler.CollectResult (existing bug)

**File:** `backend/Infrastructure/Orleans/State/Transactions/GrainTransactionHandler.cs`

Метод `CollectResult()` всегда возвращает пустой список side effects. Исправить:

```csharp
// БЫЛО:
SideEffects = new List<ISideEffect>()

// СТАЛО:
SideEffects = _sideEffects.ToList()
```

Также в `OnSuccess()` добавить `_sideEffects.Clear();` после `_states.Clear();`.
В `OnFailure()` добавить `_sideEffects.Clear();` после `_states.Clear();`.

---

## Step 2 — Add OnSuccessCallbacks to TransactionContext

**File:** `backend/Infrastructure/Orleans/State/Transactions/TransactionContext.cs`

Добавить поле (без `[Id]` — не сериализуется, только in-memory):

```csharp
// Callbacks called within the Postgres transaction on commit, before pgTransaction.CommitAsync().
// Used by transactional side effects to atomically delete from side_effects_processing.
public List<Func<NpgsqlTransaction, Task>> OnSuccessCallbacks { get; } = new();
```

Добавить using: `using Npgsql;`

---

## Step 3 — Call OnSuccessCallbacks in Transactions.Run()

**File:** `backend/Infrastructure/Orleans/State/Transactions/Transactions.cs`

В блоке с `pgTransaction`, после `_sideEffectsStorage.Write(...)` и ДО `transaction.CommitAsync()`:

```csharp
if (result.SideEffects.Count != 0)
    await _sideEffectsStorage.Write(transaction, result.SideEffects);

// NEW — call transactional completion callbacks within same Postgres transaction
foreach (var callback in context.OnSuccessCallbacks)
    await callback(transaction);

await transaction.CommitAsync();
```

---

## Step 4 — ITransactionalSideEffect marker interface

**File:** `backend/Infrastructure/Orleans/SideEffects/ISideEffect.cs`

Добавить маркерный интерфейс:

```csharp
// Marker interface. Implementations are executed inside Transactions.Run().
// Deletion from side_effects_processing is atomic with the transaction's Postgres commit.
public interface ITransactionalSideEffect : ISideEffect { }
```

---

## Step 5 — SideEffectEntry + Updated ISideEffectsStorage

**File:** `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`

Добавить класс `SideEffectEntry`:

```csharp
public class SideEffectEntry
{
    public required Guid Id { get; init; }
    public required ISideEffect Effect { get; init; }
    public required int RetryCount { get; init; }
}
```

Обновить `ISideEffectsStorage`:

```csharp
public interface ISideEffectsStorage
{
    // Write new side effects into side_effects_queue (called within Transactions.Run() pgTransaction)
    Task Write(NpgsqlTransaction transaction, IReadOnlyList<ISideEffect> effects);

    // Atomically move up to `count` oldest entries from queue → processing. Returns them.
    Task<IReadOnlyList<SideEffectEntry>> Read(int count);

    // Delete from side_effects_processing within an existing Postgres transaction (for ITransactionalSideEffect)
    Task CompleteProcessing(NpgsqlTransaction transaction, Guid id);

    // Delete from side_effects_processing standalone (for simple ISideEffect)
    Task CompleteProcessing(Guid id);

    // Move from side_effects_processing → side_effects_retry_queue (or delete if max retries exceeded)
    Task FailProcessing(Guid id, int retryCount, int maxRetryCount, float incrementalRetryDelaySeconds);

    // Move entries from side_effects_retry_queue → side_effects_queue where retry_after <= now
    Task RequeueReady();
}
```

---

## Step 6 — Используй IStateSerializer

---

## Step 7 — SideEffectsSetup (table creation)

**File:** создать `backend/Infrastructure/Orleans/SideEffects/SideEffectsSetup.cs`

```csharp
using Common.Extensions;
using Npgsql;

namespace Infrastructure;

public class SideEffectsSetup
{
    public SideEffectsSetup(IDbSource dbSource)
    {
        _dbSource = dbSource;
    }

    private readonly IDbSource _dbSource;

    public async Task Run()
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();

        await CreateIfNotExists(connection, "side_effects_queue", @"
            CREATE TABLE side_effects_queue (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                PRIMARY KEY (id)
            );
            CREATE INDEX ix_side_effects_queue ON side_effects_queue USING btree (created_at);
        ");

        await CreateIfNotExists(connection, "side_effects_processing", @"
            CREATE TABLE side_effects_processing (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                processing_started_at timestamptz NOT NULL,
                PRIMARY KEY (id)
            );
        ");

        await CreateIfNotExists(connection, "side_effects_retry_queue", @"
            CREATE TABLE side_effects_retry_queue (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                retry_after timestamptz NOT NULL,
                PRIMARY KEY (id)
            );
            CREATE INDEX ix_side_effects_retry_queue ON side_effects_retry_queue USING btree (retry_after);
        ");
    }

    private static async Task CreateIfNotExists(NpgsqlConnection connection, string tableName, string createSql)
    {
        var checkQuery = $@"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = '{tableName}'
            );";

        await using var checkCommand = new NpgsqlCommand(checkQuery, connection);
        var exists = (bool)(await checkCommand.ExecuteScalarAsync())!;

        if (exists)
            return;

        await using var createCommand = new NpgsqlCommand(createSql, connection);
        await createCommand.ExecuteNonQueryAsync();
    }
}
```

Добавить в .csproj:
```xml
<Compile Include="Orleans\SideEffects\SideEffectsSetup.cs" />
```

---

## Step 8 — Implement SideEffectsStorage

**File:** `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`

Полная замена содержимого:

```csharp
using Common.Extensions;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure;

public class SideEffectsStorage : ISideEffectsStorage
{
    public SideEffectsStorage(IDbSource dbSource, IStateSerializer serializer)
    {
        _dbSource = dbSource;
        _serializer = serializer;
    }

    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;

    public async Task Write(NpgsqlTransaction transaction, IReadOnlyList<ISideEffect> effects)
    {
        foreach (var effect in effects)
        {
            var payload = _serializer.Serialize(effect);

            await using var command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
                VALUES (@id, @payload::jsonb, 0, now())
            ";
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            var payloadParam = command.Parameters.AddWithValue("payload", payload);
            payloadParam.NpgsqlDbType = NpgsqlDbType.Jsonb;

            await command.ExecuteNonQueryAsync();
        }
    }

    // Atomically move oldest `count` entries from queue to processing and return them.
    // Uses CTE to prevent concurrent workers from picking the same entries.
    public async Task<IReadOnlyList<SideEffectEntry>> Read(int count)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = $@"
            WITH picked AS (
                SELECT id, payload, retry_count, created_at
                FROM side_effects_queue
                ORDER BY created_at
                LIMIT @count
                FOR UPDATE SKIP LOCKED
            ),
            inserted AS (
                INSERT INTO side_effects_processing (id, payload, retry_count, created_at, processing_started_at)
                SELECT id, payload, retry_count, created_at, now()
                FROM picked
            ),
            deleted AS (
                DELETE FROM side_effects_queue
                WHERE id IN (SELECT id FROM picked)
            )
            SELECT id, payload::text, retry_count FROM picked
        ";
        command.Parameters.AddWithValue("count", count);

        var entries = new List<SideEffectEntry>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetGuid(0);
            var payloadJson = reader.GetString(1);
            var retryCount = reader.GetInt32(2);

            try
            {
                var effect = _serializer.Deserialize(payloadJson);
                entries.Add(new SideEffectEntry { Id = id, Effect = effect, RetryCount = retryCount });
            }
            catch (Exception)
            {
                // If deserialization fails, the entry stays in processing and will be failed later.
                entries.Add(new SideEffectEntry
                {
                    Id = id,
                    Effect = new DeadLetterSideEffect(),
                    RetryCount = retryCount
                });
            }
        }

        return entries;
    }

    public async Task CompleteProcessing(NpgsqlTransaction transaction, Guid id)
    {
        await using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task CompleteProcessing(Guid id)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task FailProcessing(Guid id, int retryCount, int maxRetryCount, float incrementalRetryDelaySeconds)
    {
        if (retryCount >= maxRetryCount)
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync();
            return;
        }

        var delaySeconds = (retryCount + 1) * incrementalRetryDelaySeconds;
        var retryAfter = DateTime.UtcNow.AddSeconds(delaySeconds);
        var newRetryCount = retryCount + 1;

        await using var conn = await _dbSource.Value.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using var insertCommand = conn.CreateCommand();
        insertCommand.Transaction = tx;
        insertCommand.CommandText = @"
            INSERT INTO side_effects_retry_queue (id, payload, retry_count, created_at, retry_after)
            SELECT id, payload, @newRetryCount, created_at, @retryAfter
            FROM side_effects_processing
            WHERE id = @id
        ";
        insertCommand.Parameters.AddWithValue("id", id);
        insertCommand.Parameters.AddWithValue("newRetryCount", newRetryCount);
        insertCommand.Parameters.AddWithValue("retryAfter", retryAfter);
        await insertCommand.ExecuteNonQueryAsync();

        await using var deleteCommand = conn.CreateCommand();
        deleteCommand.Transaction = tx;
        deleteCommand.CommandText = "DELETE FROM side_effects_processing WHERE id = @id";
        deleteCommand.Parameters.AddWithValue("id", id);
        await deleteCommand.ExecuteNonQueryAsync();

        await tx.CommitAsync();
    }

    public async Task RequeueReady()
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            WITH ready AS (
                DELETE FROM side_effects_retry_queue
                WHERE retry_after <= now()
                RETURNING id, payload, retry_count, created_at
            )
            INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
            SELECT id, payload, retry_count, created_at FROM ready
            ON CONFLICT (id) DO NOTHING
        ";
        await command.ExecuteNonQueryAsync();
    }
}

// Sentinel used when payload deserialization fails — immediately fails without executing.
internal class DeadLetterSideEffect : ISideEffect
{
    public Task Execute(IOrleans orleans) =>
        Task.FromException(new InvalidOperationException("[SideEffects] Dead letter: failed to deserialize payload."));
}
```

Добавить в .csproj (если `DeadLetterSideEffect` в отдельном файле — лучше оставить в SideEffectsStorage.cs как `internal`).

---

## Step 9 — Implement SideEffectsWorker

**File:** `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`

```csharp
using Common.Extensions;
using Common.Reactive;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public class SideEffectsWorker : ICoordinatorSetupCompleted
{
    public SideEffectsWorker(
        ISideEffectsStorage storage,
        SideEffectsSetup setup,
        ITransactions transactions,
        IOrleans orleans,
        IOptions<SideEffectsOptions> options,
        ILogger<SideEffectsWorker> logger)
    {
        _storage = storage;
        _setup = setup;
        _transactions = transactions;
        _orleans = orleans;
        _options = options.Value;
        _logger = logger;
    }

    private readonly ISideEffectsStorage _storage;
    private readonly SideEffectsSetup _setup;
    private readonly ITransactions _transactions;
    private readonly IOrleans _orleans;
    private readonly SideEffectsOptions _options;
    private readonly ILogger<SideEffectsWorker> _logger;

    private int _inProgress;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await _setup.Run();
        Loop(lifetime).NoAwait();
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            try
            {
                await _storage.RequeueReady();

                var freeSlots = _options.ConcurrentExecutions - _inProgress;

                if (freeSlots > 0)
                {
                    var entries = await _storage.Read(freeSlots);

                    foreach (var entry in entries)
                        ExecuteEntry(entry, lifetime).NoAwait();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[SideEffects] Error in scan loop");
            }

            await Task.Delay(_options.ScanDelay, lifetime.Token);
        }
    }

    private async Task ExecuteEntry(SideEffectEntry entry, IReadOnlyLifetime lifetime)
    {
        Interlocked.Increment(ref _inProgress);

        try
        {
            if (entry.Effect is ITransactionalSideEffect)
            {
                var result = await _transactions.Run(async () =>
                {
                    TransactionContextProvider.Current!.OnSuccessCallbacks.Add(
                        tx => _storage.CompleteProcessing(tx, entry.Id)
                    );

                    await entry.Effect.Execute(_orleans);
                });

                if (!result.IsSuccess)
                    throw new Exception("[SideEffects] Transactional side effect failed.");
            }
            else
            {
                await entry.Effect.Execute(_orleans);
                await _storage.CompleteProcessing(entry.Id);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[SideEffects] Effect {Id} failed (attempt {RetryCount}/{MaxRetry})",
                entry.Id, entry.RetryCount + 1, _options.MaxRetryCount
            );

            try
            {
                await _storage.FailProcessing(
                    entry.Id,
                    entry.RetryCount,
                    _options.MaxRetryCount,
                    _options.IncrementalRetryDelay
                );
            }
            catch (Exception failEx)
            {
                _logger.LogError(failEx, "[SideEffects] Failed to record failure for effect {Id}", entry.Id);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _inProgress);
        }
    }
}
```

---

## Step 10 — DI Registration

**File:** `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs`

В методе `AddSideEffects()` добавить регистрацию новых типов:

```csharp
private IHostApplicationBuilder AddSideEffects()
{
    builder.Services.Configure<SideEffectsOptions>(
        builder.Configuration.GetSection("SideEffects")
    );

    builder.Add<SideEffectSerializer>()
        .As<ISideEffectSerializer>();

    builder.Add<SideEffectsSetup>();

    builder.Add<SideEffectsStorage>()
        .As<ISideEffectsStorage>();

    builder.Add<SideEffectsWorker>()
        .As<ICoordinatorSetupCompleted>();

    return builder;
}
```

Убедиться, что `ITransactions` (`Transactions`) зарегистрирован в том же `AddBase()`.
Если нет — добавить:
```csharp
builder.Add<Transactions>().As<ITransactions>();
```

---

## Step 11 — SideEffectsOptions defaults

**File:** `backend/Infrastructure/Orleans/SideEffects/SideEffectsOptions.cs`

Добавить разумные defaults:

```csharp
public class SideEffectsOptions
{
    public int ScanDelay { get; set; } = 500;              // ms между итерациями воркера
    public int ConcurrentExecutions { get; set; } = 10;    // макс. параллельных эффектов
    public int MaxRetryCount { get; set; } = 5;            // макс. попыток
    public float IncrementalRetryDelay { get; set; } = 30; // секунд * retryCount = задержка
}
```

---

## Step 12 — Add new .cs files to .csproj

Найти .csproj файл Infrastructure проекта:
```bash
grep -rl "SideEffectsStorage" backend/*.csproj backend/**/*.csproj
```

Добавить:
```xml
<Compile Include="Orleans\SideEffects\SideEffectSerializer.cs" />
<Compile Include="Orleans\SideEffects\SideEffectsSetup.cs" />
```

(остальные файлы уже могут быть в .csproj, проверить)

---

## Checklist

- [ ] Step 1: Fix CollectResult bug in GrainTransactionHandler (return _sideEffects, clear in OnSuccess/OnFailure)
- [ ] Step 2: Add OnSuccessCallbacks to TransactionContext
- [ ] Step 3: Call OnSuccessCallbacks in Transactions.Run() before CommitAsync()
- [ ] Step 4: Add ITransactionalSideEffect to ISideEffect.cs
- [ ] Step 5: Add SideEffectEntry + update ISideEffectsStorage interface
- [ ] Step 6: Create SideEffectSerializer.cs
- [ ] Step 7: Create SideEffectsSetup.cs
- [ ] Step 8: Implement SideEffectsStorage (replace stubs)
- [ ] Step 9: Implement SideEffectsWorker (replace stubs)
- [ ] Step 10: Update DI registration in ProjectsSetupExtensions.cs
- [ ] Step 11: Add defaults to SideEffectsOptions
- [ ] Step 12: Add new files to .csproj
