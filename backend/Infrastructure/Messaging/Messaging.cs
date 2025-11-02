using Common.Reactive;

namespace Infrastructure;

public interface IMessaging
{
    IMessageQueueClient Queue { get; }
    IMessagePipeClient Pipe { get; }

    Task Start(IReadOnlyLifetime lifetime);
}

public class Messaging : IMessaging
{
    public Messaging(IMessageQueueClient queue, IMessagePipeClient pipe)
    {
        Queue = queue;
        Pipe = pipe;
    }

    public IMessageQueueClient Queue { get; }
    public IMessagePipeClient Pipe { get; }

    public Task Start(IReadOnlyLifetime lifetime)
    {
        return Task.WhenAll(Queue.Start(lifetime), Pipe.Start(lifetime));
    }
}