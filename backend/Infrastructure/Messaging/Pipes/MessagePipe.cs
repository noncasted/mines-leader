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

    private static readonly TimeSpan LivenessCheckTimeout = TimeSpan.FromMilliseconds(500);

    private readonly ILogger<RuntimePipe> _logger;
    private readonly IRuntimePipeConfig _config;

    private IRuntimePipeObserver? _observer;
    private Guid? _observerId;
    private DateTime _setDate;

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (_observer != null)
        {
            var timeSinceLastUpdate = DateTime.UtcNow - _setDate;
            var keepAlive = TimeSpan.FromMinutes(_config.Value.ObserverKeepAliveMinutes);

            if (timeSinceLastUpdate < keepAlive)
                DelayDeactivation(keepAlive - timeSinceLastUpdate);
        }

        return Task.CompletedTask;
    }

    public Task BindObserver(Guid observerId, IRuntimePipeObserver observer)
    {
        _logger.LogTrace("[Messaging] [RuntimePipe] Binding observer {ObserverId} to pipe {PipeId}",
            observerId, this.GetPrimaryKeyString());
        _observer = observer;
        _observerId = observerId;
        _setDate = DateTime.UtcNow;
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

        try
        {
            await observer.Ping().WaitAsync(LivenessCheckTimeout);
            return true;
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
        using var activity = TraceExtensions.MessagingRuntimePipe.StartActivity("RuntimePipe.Send");
        activity?.SetTag("messaging.pipe", pipeId);
        activity?.SetTag("message.type", message.GetType().Name);
        activity?.SetTag("pipe.timeout", _config.Value.SendTimeoutSeconds);

        BackendMetrics.PipeRequestSent.Add(1);
        using var watch = MetricWatch.Start(BackendMetrics.PipeDuration);

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
            throw new InvalidOperationException($"No observer bound for pipe {pipeId}");
        }

        try
        {
            var timeout = TimeSpan.FromSeconds(_config.Value.SendTimeoutSeconds);
            var response = await observer.Send<TResponse>(message).WaitAsync(timeout);

            _logger.LogTrace(
                "[Messaging] [RuntimePipe] Successfully received response {ResponseType} for message {MessageType} on pipe {PipeId} from observer {ObserverId}",
                typeof(TResponse).Name, message.GetType().Name, pipeId, observerId);
            return response;
        }
        catch (TimeoutException ex)
        {
            DiscardObserverIfSame(observerId);

            activity?.SetStatus(ActivityStatusCode.Error, "Pipe send timed out");
            BackendMetrics.PipeTimeout.Add(1);

            _logger.LogError(ex,
                "[Messaging] [RuntimePipe] Timed out processing request-response message {MessageType} on pipe {PipeId} with observer {ObserverId}",
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
