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
                var result = await _transactions
                    .Create(() => entry.Effect.Execute(_orleans))
                    .WithCallback(npgsqlTransaction => _storage.CompleteProcessing(npgsqlTransaction, entry.Id))
                    .Run();

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