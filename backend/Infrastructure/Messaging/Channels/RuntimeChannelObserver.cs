namespace Infrastructure;

public interface IRuntimeChannelObserver : IGrainObserver
{
    Task Send(object message);
}

public class RuntimeChannelObserver : IRuntimeChannelObserver
{
    public RuntimeChannelObserver(Action<object> onMessage)
    {
        _onMessage = onMessage;
    }

    private readonly Action<object> _onMessage;

    public Guid Id { get; } = Guid.NewGuid();

    public Task Send(object message)
    {
        _onMessage(message);
        return Task.CompletedTask;
    }
}