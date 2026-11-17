using Microsoft.Extensions.Logging;

namespace Infrastructure;

// One entry taken into processing: the effect and its completion, or its failure recorded for a retry or the dead
// letter. The worker runs it for every entry it reads, the tests run it without a worker.
public class SideEffectExecution
{
    public SideEffectExecution(
        ISideEffectsStorage storage,
        ITransactions transactions,
        IOrleans orleans,
        ISideEffectsConfig config,
        ILogger logger)
    {
        _storage = storage;
        _transactions = transactions;
        _orleans = orleans;
        _config = config;
        _logger = logger;
    }

    private readonly ISideEffectsStorage _storage;
    private readonly ITransactions _transactions;
    private readonly IOrleans _orleans;
    private readonly ISideEffectsConfig _config;
    private readonly ILogger _logger;

    // Returns the error of the effect, null when it succeeded. Never throws: the failure is recorded in the storage.
    public async Task<Exception?> Execute(SideEffectEntry entry)
    {
        try
        {
            await Run(entry);
            SideEffectTelemetry.Processed.Add(1);
            return null;
        }
        catch (Exception e)
        {
            await Fail(entry, e);
            return e;
        }
    }

    private async Task Run(SideEffectEntry entry)
    {
        if (entry.Effect is ITransactionalSideEffect)
        {
            var result = await _transactions
                               .CreateBuilder(() => entry.Effect.Execute(_orleans))
                               .WithCallback(transaction => _storage.CompleteProcessing(transaction, entry.Id))
                               .Run();

            result.ThrowIfFailed();
            return;
        }

        await entry.Effect.Execute(_orleans);
        await _storage.CompleteProcessing(entry.Id);
    }

    private async Task Fail(SideEffectEntry entry, Exception error)
    {
        var options = _config.Value;

        _logger.LogError(error,
            "[SideEffects] Effect {Id} failed (attempt {Attempt}/{MaxAttempts})",
            entry.Id, entry.RetryCount + 1, options.MaxRetryCount);

        if (entry.RetryCount > 0)
            SideEffectTelemetry.Retry.Add(1);

        SideEffectTelemetry.Failed.Add(1);

        try
        {
            await _storage.FailProcessing(entry.Id,
                entry.RetryCount,
                options.MaxRetryCount,
                options.IncrementalRetryDelay,
                error.Message);
        }
        catch (Exception failError)
        {
            // The entry stays in processing: the stuck check of a worker requeues it.
            _logger.LogError(failError, "[SideEffects] Failed to record failure for effect {Id}", entry.Id);
        }
    }
}
