using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

// Sends a request through a pipe and repeats it, with a growing delay, while it reaches no handler.
internal class RuntimePipeSender
{
    public RuntimePipeSender(Lazy<IRuntimePipeConfig> config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    private readonly Lazy<IRuntimePipeConfig> _config;
    private readonly ILogger _logger;

    public async Task<TResponse> Send<TResponse>(IRuntimePipeId id, IRuntimePipe pipe, object message)
    {
        var options = _config.Value.Value;

        Exception? lastException = null;

        for (var attempt = 0; attempt <= options.SendRetryCount; attempt++)
        {
            try
            {
                return await pipe.Send<TResponse>(message);
            }
            catch (Exception e) when (IsTransient(e))
            {
                lastException = e;

                if (attempt < options.SendRetryCount)
                {
                    RuntimePipeTelemetry.Retry.Add(1);
                    var delay = options.SendRetryBaseDelayMs * (1 << attempt);

                    _logger.LogWarning(e,
                        "[Messaging] [Pipe] Send to {PipeId} failed (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                        id.ToRaw(), attempt + 1, options.SendRetryCount + 1, delay);

                    await Task.Delay(delay);
                }
            }
        }

        throw lastException!;
    }

    // Only a request that reached no handler is sent again. A timeout or a transport failure may come after the
    // handler ran: repeating it would run a non-idempotent handler twice.
    private static bool IsTransient(Exception e)
    {
        var current = e;

        while (current != null)
        {
            if (current.Message.Contains(RuntimePipe.NoObserverPrefix, StringComparison.Ordinal))
                return true;

            current = current.InnerException;
        }

        return false;
    }
}
