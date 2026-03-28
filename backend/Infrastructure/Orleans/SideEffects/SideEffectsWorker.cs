using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public class SideEffectsWorker : IHostedService
{
    public SideEffectsWorker(
        ISideEffectsStorage storage,
        ITransactions transactions,
        IOrleans orleans,
        IServiceLoopObserver loopObserver,
        ISideEffectsConfig config,
        ILogger<SideEffectsWorker> logger)
    {
        _storage = storage;
        _transactions = transactions;
        _orleans = orleans;
        _loopObserver = loopObserver;
        _config = config;
        _logger = logger;
    }

    private readonly ISideEffectsStorage _storage;
    private readonly ITransactions _transactions;
    private readonly IOrleans _orleans;
    private readonly IServiceLoopObserver _loopObserver;
    private readonly ISideEffectsConfig _config;
    private readonly ILogger<SideEffectsWorker> _logger;

    private int _inProgress;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var lifetime = cancellationToken.ToLifetime();
        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        await _loopObserver.IsOrleansStarted.WaitTrue(lifetime);

        while (lifetime.IsTerminated == false)
        {
            try
            {
                await _storage.RequeueReady();

                var options = _config.Value;
                var freeSlots = options.ConcurrentExecutions - _inProgress;

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

            await Task.Delay(_config.Value.ScanDelay, lifetime.Token);
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
                    .CreateBuilder(() => entry.Effect.Execute(_orleans))
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
            var options = _config.Value;

            _logger.LogError(e,
                "[SideEffects] Effect {Id} failed (attempt {RetryCount}/{MaxRetry})",
                entry.Id, entry.RetryCount + 1, options.MaxRetryCount
            );

            try
            {
                await _storage.FailProcessing(
                    entry.Id,
                    entry.RetryCount,
                    options.MaxRetryCount,
                    options.IncrementalRetryDelay
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

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}