using Microsoft.Extensions.Logging;
using Orleans.Concurrency;

namespace Infrastructure.Messaging;

[Reentrant]
public class MessageStream : Grain, IMessageStream
{
    public MessageStream(ILogger<MessageStream> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<MessageStream> _logger;

    private IMessageStreamObserver? _observer;

    public Task BindObserver(IMessageStreamObserver observer)
    {
        _observer = observer;
        return Task.CompletedTask;
    }

    public async Task Send(IClusterMessage message)
    {
        var hasObserver = await CheckObserver();

        if (hasObserver == false)
            return;

        await _observer!.Send(message);
    }

    public async Task<TResponse> Send<TResponse>(IClusterMessage message) where TResponse : IClusterMessage
    {
        var hasObserver = await CheckObserver();

        if (hasObserver == false)
            throw new Exception($"No observer for stream {this.GetPrimaryKeyString()}");

        return await _observer!.Send<TResponse>(message);
    }

    private async ValueTask<bool> CheckObserver()
    {
        if (_observer != null)
            return true;

        await Task.Delay(TimeSpan.FromSeconds(10));

        if (_observer == null)
        {
            var id = this.GetPrimaryKeyString();
            _logger.LogWarning("[Messaging] [Stream] No observer for stream {StreamId}", id);
            return false;
        }

        return true;
    }
}