using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure;

public interface ISideEffectsStorage
{
    Task Write(ISideEffect effects);

    // Write new side effects into side_effects_queue (called within Transactions.Process() pgTransaction)
    Task Write(NpgsqlTransaction transaction, IReadOnlyList<ISideEffect> effects);

    // Same insert as Write(transaction, effects) but not executed: Transactions.Process puts it into one batch.
    NpgsqlBatchCommand BuildWriteCommand(IReadOnlyList<ISideEffect> effects);

    // Atomically move up to `count` oldest entries from queue → processing. Returns them.
    Task<IReadOnlyList<SideEffectEntry>> Read(int count);

    // Delete from side_effects_processing within an existing Postgres transaction (for ITransactionalSideEffect)
    Task CompleteProcessing(NpgsqlTransaction transaction, Guid id);

    // Delete from side_effects_processing standalone (for simple ISideEffect)
    Task CompleteProcessing(Guid id);

    // Move from side_effects_processing → side_effects_retry_queue, or to the dead letter when this failure is attempt
    // maxAttempts (retryCount: the failed attempts before it)
    Task FailProcessing(
        Guid id,
        int retryCount,
        int maxAttempts,
        float incrementalRetryDelaySeconds,
        string? errorMessage = null);

    // Move entries from side_effects_retry_queue → side_effects_queue where retry_after <= now
    Task RequeueReady();

    // Move entries stuck in processing longer than `age` back to queue: every worker at its stuck check, and the
    // console on an action of an operator, both with StuckThresholdMinutes, so an entry a live worker touches stays.
    Task RequeueStuckOlderThan(TimeSpan age);

    // Marks the entries a worker is still executing as alive (processing_started_at = now), so RequeueStuckOlderThan
    // of any worker takes only the entries of a worker that is gone.
    Task TouchProcessing(IReadOnlyCollection<Guid> ids);

    // Monitor: get counts for all three tables
    Task<SideEffectsStats> GetStats();

    // Monitor: get entries from retry queue for display
    Task<IReadOnlyList<RetryQueueEntry>> GetRetryEntries(int limit);

    // Monitor: drop a single entry from retry queue
    Task DropRetryEntry(Guid id);

    // Monitor: move a single entry from retry queue back to main queue
    Task RequeueRetryEntry(Guid id);
}

public class SideEffectsStorage : ISideEffectsStorage
{
    public SideEffectsStorage(IDbSource dbSource, IStateSerializer serializer, ILogger<SideEffectsStorage> logger)
    {
        _writer = new SideEffectsQueueWriter(dbSource, serializer, logger);
        _reader = new SideEffectsQueueReader(dbSource, serializer, logger);
        _processing = new SideEffectsProcessing(dbSource, logger);
        _requeue = new SideEffectsRequeue(dbSource, logger);
        _monitor = new SideEffectsMonitorQueries(dbSource);
    }

    private readonly SideEffectsQueueWriter _writer;
    private readonly SideEffectsQueueReader _reader;
    private readonly SideEffectsProcessing _processing;
    private readonly SideEffectsRequeue _requeue;
    private readonly SideEffectsMonitorQueries _monitor;

    public Task Write(ISideEffect effects) => _writer.Write(effects);

    public Task Write(NpgsqlTransaction transaction, IReadOnlyList<ISideEffect> effects) =>
        _writer.Write(transaction, effects);

    public NpgsqlBatchCommand BuildWriteCommand(IReadOnlyList<ISideEffect> effects) =>
        _writer.BuildWriteCommand(effects);

    public Task<IReadOnlyList<SideEffectEntry>> Read(int count) => _reader.Read(count);

    public Task CompleteProcessing(NpgsqlTransaction transaction, Guid id) =>
        _processing.CompleteProcessing(transaction, id);

    public Task CompleteProcessing(Guid id) => _processing.CompleteProcessing(id);

    public Task FailProcessing(
        Guid id,
        int retryCount,
        int maxAttempts,
        float incrementalRetryDelaySeconds,
        string? errorMessage = null)
    {
        return _processing.FailProcessing(id, retryCount, maxAttempts, incrementalRetryDelaySeconds, errorMessage);
    }

    public Task TouchProcessing(IReadOnlyCollection<Guid> ids) => _processing.TouchProcessing(ids);

    public Task RequeueStuckOlderThan(TimeSpan age) => _requeue.RequeueStuckOlderThan(age);

    public Task RequeueReady() => _requeue.RequeueReady();

    public Task<SideEffectsStats> GetStats() => _monitor.GetStats();

    public Task<IReadOnlyList<RetryQueueEntry>> GetRetryEntries(int limit) => _monitor.GetRetryEntries(limit);

    public Task DropRetryEntry(Guid id) => _monitor.DropRetryEntry(id);

    public Task RequeueRetryEntry(Guid id) => _monitor.RequeueRetryEntry(id);
}
