using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IRuntimePipe : IGrainWithStringKey
{
    Task BindObserver(IRuntimePipeObserver observer);
    Task<TResponse> Send<TResponse>(object message);
}

public class RuntimePipe : Grain, IRuntimePipe
{
    public RuntimePipe(ILogger<RuntimePipe> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<RuntimePipe> _logger;

    private IRuntimePipeObserver? _observer;
    private DateTime _setDate;

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        var timeSinceLastUpdate = DateTime.UtcNow - _setDate;

        if (timeSinceLastUpdate > TimeSpan.FromMinutes(3))
            return;

        throw new Exception("[Messaging] [RuntimePipe] Keeping pipe alive because observer was recently set");
    }

    public Task BindObserver(IRuntimePipeObserver observer)
    {
        _logger.LogTrace("[Messaging] [RuntimePipe] Binding observer to pipe {PipeId}", this.GetPrimaryKeyString());
        _observer = observer;
        _setDate = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public async Task<TResponse> Send<TResponse>(object message)
    {
        _logger.LogTrace(
            "[Messaging] [RuntimePipe] Sending request-response message {MessageType} expecting {ResponseType} to pipe {PipeId}",
            message.GetType().Name, typeof(TResponse).Name, this.GetPrimaryKeyString()
        );

        if (_observer == null)
        {
            _logger.LogError(
                "[Messaging] [RuntimePipe] No observer bound for request-response message {MessageType} on pipe {PipeId}",
                message.GetType().Name, this.GetPrimaryKeyString()
            );
            throw new Exception($"No observer for stream {this.GetPrimaryKeyString()}");
        }

        try
        {
            var response = await _observer!.Send<TResponse>(message);
            _logger.LogTrace(
                "[Messaging] [RuntimePipe] Successfully received response {ResponseType} for message {MessageType} on pipe {PipeId}",
                typeof(TResponse).Name, message.GetType().Name, this.GetPrimaryKeyString()
            );
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Messaging] [RuntimePipe] Failed to process request-response message {MessageType} on pipe {PipeId}",
                message.GetType().Name, this.GetPrimaryKeyString()
            );
            throw;
        }
    }
}
