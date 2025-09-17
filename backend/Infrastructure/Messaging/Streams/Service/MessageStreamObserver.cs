using Infrastructure.Messaging;

namespace Service;

public class MessageStreamObserver : IMessageStreamObserver
{
    public MessageStreamObserver(Action<object> onMessage, Func<object, Task<object>> onMessageHandle)
    {
        _onMessage = onMessage;
        _onMessageHandle = onMessageHandle;
    }

    private readonly Action<object> _onMessage;
    private readonly Func<object, Task<object>> _onMessageHandle;

    public Task Send(object message)
    {
        _onMessage(message);
        return Task.CompletedTask;
    }

    public async Task<TResponse> Send<TResponse>(object message)
    {
        var response = await _onMessageHandle(message);
        
        if (response is not TResponse typedResponse)
            throw new InvalidCastException($"Expected {typeof(TResponse)}, but got {response.GetType()}");
        
        return typedResponse;
    }
}