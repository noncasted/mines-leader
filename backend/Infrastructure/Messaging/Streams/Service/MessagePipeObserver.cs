using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

public class MessagePipeObserver : IMessagePipeObserver
{
    public MessagePipeObserver(ILogger logger)
    {
        _logger = logger;
    }

    private readonly ILogger _logger;

    private Action<object>? _oneWayHandler;
    private Func<object, Task<object>>? _responseHandler;

    public void BindOneWayHandler(Action<object> handler)
    {
        if (_oneWayHandler != null)
            throw new InvalidOperationException("One-way handler is already bound.");

        _oneWayHandler = handler;
    }

    public void BindResponseHandler(Func<object, Task<object>> handler)
    {
        if (_responseHandler != null)
            throw new InvalidOperationException("Response handler is already bound.");

        _responseHandler = handler;
    }

    public Task Send(object message)
    {
        if (_oneWayHandler == null)
        {
            _logger.LogWarning("[Messaging] [Pipe] No one-way handler bound to process message of type {MessageType}",
                message.GetType()
            );

            return Task.CompletedTask;
        }

        _oneWayHandler(message);
        return Task.CompletedTask;
    }

    public async Task<TResponse> Send<TResponse>(object message)
    {
        if (_responseHandler == null)
            throw new InvalidOperationException("[Messaging] [Pipe] No one-way handler bound to process message.");

        var response = await _responseHandler(message);

        if (response is not TResponse typedResponse)
            throw new InvalidCastException($"Expected {typeof(TResponse)}, but got {response.GetType()}");

        return typedResponse;
    }
}