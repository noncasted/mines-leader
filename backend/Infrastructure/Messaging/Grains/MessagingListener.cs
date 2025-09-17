using Common;

namespace Infrastructure.Messaging;

public class MessagingListener : IMessagingListener
{
    private readonly ViewableDelegate<IClusterMessage> _messageReceived = new();
    private readonly ViewableDelegate<IClusterMessage, Action<Task<IClusterMessage>>> _responseMessageReceived = new();

    public IViewableDelegate<IClusterMessage> MessageReceived => _messageReceived;

    public IViewableDelegate<IClusterMessage, Action<Task<IClusterMessage>>> ResponseMessageReceived =>
        _responseMessageReceived;

    public Task Ping() => Task.CompletedTask;

    public Task Send(IClusterMessage message)
    {
        _messageReceived.Invoke(message);
        return Task.CompletedTask;
    }

    public Task<TResponse> Send<TResponse>(IClusterMessage message)
    {
        var completion = new TaskCompletionSource<TResponse>();

        _responseMessageReceived.Invoke(message, task =>
        {
            Task.Run(async () =>
            {
                var result = await task;

                if (result is not TResponse response)
                    throw new InvalidCastException($"Expected {typeof(TResponse)}, but got {result.GetType()}");

                completion.TrySetResult(response);
            });
        });

        return completion.Task;
    }
}