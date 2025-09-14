namespace Infrastructure.Messaging;

public class MessageQueue : Grain, IMessageQueue
{
    public Task Send(IClusterMessage message)
    {
        throw new NotImplementedException();
    }
}