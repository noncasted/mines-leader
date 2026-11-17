using System.Diagnostics;
using Common.Extensions;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;

namespace Infrastructure;

public interface IRuntimePipe : IGrainWithStringKey
{
    Task BindObserver(Guid observerId, IRuntimePipeObserver observer);
    Task UnbindObserver(Guid observerId);
    Task<TResponse> Send<TResponse>(object message);
    Task<bool> HasObserver();
}

[Reentrant]
public class RuntimePipe : Grain, IRuntimePipe
{
    public RuntimePipe(ILogger<RuntimePipe> logger, IRuntimePipeConfig config)
    {
        _logger = logger;
        _config = config;
    }

    // The start of the failure of a request that reached no handler: the only one a sender may repeat.
    public const string NoObserverPrefix = "[Messaging] [RuntimePipe] No observer bound";

    private static readonly TimeSpan LivenessCheckTimeout = TimeSpan.FromMilliseconds(500);

    private readonly ILogger<RuntimePipe> _logger;
    private readonly IRuntimePipeConfig _config;

    private IRuntimePipeObserver? _observer;
    private Guid? _observerId;

    public Task BindObserver(Guid observerId, IRuntimePipeObserver observer)
    {
        _logger.LogTrace("[Messaging] [RuntimePipe] Binding observer {ObserverId} to pipe {PipeId}",
            observerId, this.GetPrimaryKeyString());
        _observer = observer;
        _observerId = observerId;

        // The observer lives only in this activation's memory and rebinds itself periodically: keep the activation
        // between rebinds. This must happen here, DelayDeactivation called from OnDeactivateAsync does not stop
        // a deactivation that has already started.
        DelayDeactivation(TimeSpan.FromMinutes(_config.Value.ObserverKeepAliveMinutes));
        return Task.CompletedTask;
    }

    public Task UnbindObserver(Guid observerId)
    {
        if (_observerId != observerId)
            return Task.CompletedTask;

        _observer = null;
        _observerId = null;

        _logger.LogTrace("[Messaging] [RuntimePipe] Unbound observer {ObserverId} from pipe {PipeId}",
            observerId, this.GetPrimaryKeyString());
        return Task.CompletedTask;
    }

    public async Task<bool> HasObserver()
    {
        var observer = _observer;
        var observerId = _observerId;

        if (observer == null || observerId == null)
            return false;

        var ping = observer.Ping();

        try
        {
            await ping.WaitAsync(LivenessCheckTimeout);
            return true;
        }
        catch (TimeoutException e)
        {
            // The ping of an observer busy with a request waits behind it: slow is not dead, the observer stays.
            _logger.LogWarning(e,
                "[Messaging] [RuntimePipe] Observer {ObserverId} on pipe {PipeId} did not answer the liveness check in time",
                observerId, this.GetPrimaryKeyString());

            LateTasks.Observe(ping, _logger,
                "[Messaging] [RuntimePipe] A timed out liveness check of observer {ObserverId} on pipe {PipeId} failed later",
                observerId, this.GetPrimaryKeyString());
            return false;
        }
        catch (Exception e)
        {
            DiscardObserverIfSame(observerId);

            _logger.LogWarning(e,
                "[Messaging] [RuntimePipe] Observer {ObserverId} on pipe {PipeId} failed liveness check",
                observerId, this.GetPrimaryKeyString());
            return false;
        }
    }

    public async Task<TResponse> Send<TResponse>(object message)
    {
        var pipeId = this.GetPrimaryKeyString();
        using var activity = RuntimePipeTelemetry.Source.StartActivity("RuntimePipe.Send");
        activity?.SetTag("messaging.pipe", pipeId);
        activity?.SetTag("message.type", message.GetType().Name);
        activity?.SetTag("pipe.timeout", _config.Value.SendTimeoutSeconds);

        RuntimePipeTelemetry.RequestSent.Add(1);
        using var watch = MetricWatch.Start(RuntimePipeTelemetry.Duration);

        _logger.LogTrace(
            "[Messaging] [RuntimePipe] Sending request-response message {MessageType} expecting {ResponseType} to pipe {PipeId}",
            message.GetType().Name, typeof(TResponse).Name, pipeId);

        var observer = _observer;
        var observerId = _observerId;

        if (observer == null || observerId == null)
        {
            _logger.LogError(
                "[Messaging] [RuntimePipe] No observer bound for request-response message {MessageType} on pipe {PipeId}",
                message.GetType().Name, pipeId);
            throw new InvalidOperationException($"{NoObserverPrefix} for pipe {pipeId}");
        }

        var request = observer.Send<TResponse>(message);

        try
        {
            var timeout = TimeSpan.FromSeconds(_config.Value.SendTimeoutSeconds);
            var response = await request.WaitAsync(timeout);

            _logger.LogTrace(
                "[Messaging] [RuntimePipe] Successfully received response {ResponseType} for message {MessageType} on pipe {PipeId} from observer {ObserverId}",
                typeof(TResponse).Name, message.GetType().Name, pipeId, observerId);
            return response;
        }
        catch (TimeoutException ex)
        {
            // A slow handler is not a dead observer: it stays bound, the request may still complete there.
            activity?.SetStatus(ActivityStatusCode.Error, "Pipe send timed out");
            RuntimePipeTelemetry.Timeout.Add(1);

            _logger.LogError(ex,
                "[Messaging] [RuntimePipe] Timed out processing request-response message {MessageType} on pipe {PipeId} with observer {ObserverId}",
                message.GetType().Name, pipeId, observerId);

            LateTasks.Observe(request, _logger,
                "[Messaging] [RuntimePipe] A timed out request {MessageType} on pipe {PipeId} failed later in observer {ObserverId}",
                message.GetType().Name, pipeId, observerId);
            throw;
        }
        catch (Exception ex) when (IsApplicationFailure(ex))
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex,
                "[Messaging] [RuntimePipe] Handler rejected request-response message {MessageType} on pipe {PipeId} with observer {ObserverId}",
                message.GetType().Name, pipeId, observerId);
            throw;
        }
        catch (Exception ex)
        {
            DiscardObserverIfSame(observerId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex,
                "[Messaging] [RuntimePipe] Transport failure processing request-response message {MessageType} on pipe {PipeId} with observer {ObserverId}",
                message.GetType().Name, pipeId, observerId);
            throw;
        }
    }

    private static bool IsApplicationFailure(Exception ex)
    {
        var current = ex;

        while (current != null)
        {
            if (current is InvalidCastException or ArgumentException or NotSupportedException)
                return true;

            if (current.Message.Contains(RuntimePipeObserver.HandlerFailurePrefix, StringComparison.Ordinal))
                return true;

            current = current.InnerException;
        }

        return false;
    }

    private void DiscardObserverIfSame(Guid? observerId)
    {
        if (_observerId != observerId)
            return;

        _observer = null;
        _observerId = null;

        _logger.LogWarning("[Messaging] [RuntimePipe] Discarded stale observer {ObserverId} for pipe {PipeId} after send failure",
            observerId, this.GetPrimaryKeyString());
    }
}
