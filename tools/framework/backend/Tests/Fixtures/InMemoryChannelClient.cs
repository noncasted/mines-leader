using Infrastructure;
using Infrastructure.State;
using Internal;
using Newtonsoft.Json;

namespace Framework.Tests.Fixtures;

// A runtime channel client without Orleans: delivers to the listeners present at the moment of the delivery, as the
// channel grain does, and reports a gap on demand.
public class InMemoryChannelClient : IRuntimeChannelClient
{
    private readonly List<(Func<object, Task> Listener, Action? OnGap)> _listeners = [];
    private readonly List<object> _published = [];

    public IReadOnlyList<object> Published => _published;

    public Task Start(IReadOnlyLifetime lifetime) => Task.CompletedTask;

    public Task Listen<T>(IReadOnlyLifetime lifetime, IRuntimeChannelId id, Func<T, Task> listener,
        Action? onGapDetected = null)
    {
        _listeners.Add((message => listener((T)message), onGapDetected));
        return Task.CompletedTask;
    }

    public Task Publish(IRuntimeChannelId id, object message)
    {
        _published.Add(message);
        Deliver(message);
        return Task.CompletedTask;
    }

    public void Deliver(object message)
    {
        foreach (var (listener, _) in _listeners.ToArray())
            listener(message).GetAwaiter().GetResult();
    }

    public void ReportGap()
    {
        foreach (var (_, onGap) in _listeners.ToArray())
            onGap?.Invoke();
    }
}
