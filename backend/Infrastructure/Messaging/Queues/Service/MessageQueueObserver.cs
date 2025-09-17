using Infrastructure.Messaging;

namespace Service;

public class MessageQueueObserver : IMessageQueueObserver
{
    public MessageQueueObserver(Action<IClusterMessage> onMessage)
    {
        _onMessage = onMessage;
    }

    private readonly Action<IClusterMessage> _onMessage;

    public Task Send(IReadOnlyList<IClusterMessage> messages)
    {
        foreach (var message in messages)
            _onMessage(message);
        
        return Task.CompletedTask;
    }
}