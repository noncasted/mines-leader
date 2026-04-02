namespace Infrastructure;

public interface IDurableQueueObserver : IGrainObserver
{
    Task Send(IReadOnlyList<object> messages);
}

public class DurableQueueObserver : IDurableQueueObserver
{
    public DurableQueueObserver(Action<object> onMessage)
    {
        _onMessage = onMessage;
    }

    private readonly Action<object> _onMessage;

    public Guid Id { get; } = Guid.NewGuid();

    public Task Send(IReadOnlyList<object> messages)
    {
        foreach (var message in messages)
            _onMessage(message);

        return Task.CompletedTask;
    }
}