namespace Infrastructure;

public interface IDurableQueueObserver : IGrainObserver
{
    Task Send(object message);
}

public class DurableQueueObserver : IDurableQueueObserver
{
    public DurableQueueObserver(Action<object> onMessage)
        : this(message => {
            onMessage(message);
            return Task.CompletedTask;
        })
    {
    }

    public DurableQueueObserver(Func<object, Task> onMessage)
    {
        _onMessage = onMessage;
    }

    private readonly Func<object, Task> _onMessage;

    public Guid Id { get; } = Guid.NewGuid();

    public Task Send(object message)
    {
        return _onMessage(message);
    }
}
