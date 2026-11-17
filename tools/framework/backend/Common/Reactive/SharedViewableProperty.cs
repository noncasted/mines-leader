namespace Internal;

// A value of a singleton of the host that several threads read and subscribe to: the circuits of the console advise and
// leave on their own threads while the host sets the value from the thread pool or a channel delivery. ViewableProperty is
// not thread-safe (its listeners are a plain list), so this one takes its lock around the value, every subscription and the
// end of every subscription. The listeners run under the lock: they take the value and post their work (InvokeAsync of a
// page), they never wait for another thread.
public interface ISharedViewableProperty<T> : IViewableProperty<T>
{
    // Preferred over the View extension by the compiler: the current value and the subscription are taken under one lock,
    // no Set goes between them and delivers its value before an older one.
    void View(IReadOnlyLifetime lifetime, Action<T> listener);
}

public class SharedViewableProperty<T> : ISharedViewableProperty<T>
{
    public SharedViewableProperty(T value)
    {
        _value = new ViewableProperty<T>(value);
    }

    // Also the lock of an owner around what it decides before a Set (DeploymentState: the epoch and the watermark): one lock,
    // so a listener that calls back into the owner takes no second one in the other order.
    protected readonly object _gate = new();

    private readonly ViewableProperty<T> _value;

    public T Value => _value.Value;

    public IReadOnlyLifetime ValueLifetime
    {
        get
        {
            lock (_gate)
                return _value.ValueLifetime;
        }
    }

    public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, T> handler)
    {
        // The subscription has its own lifetime ended under the lock: the end of the subscriber's lifetime comes on any thread.
        var subscription = new Lifetime();

        lock (_gate)
            _value.Advise(subscription, handler);

        lifetime.Listen(() =>
        {
            lock (_gate)
                subscription.Terminate();
        });
    }

    public void View(IReadOnlyLifetime lifetime, Action<T> listener)
    {
        lock (_gate)
        {
            Advise(lifetime, (_, value) => listener(value));
            listener(_value.Value);
        }
    }

    public void Set(T value)
    {
        lock (_gate)
            _value.Set(value);
    }

    public void Dispose()
    {
        lock (_gate)
            _value.Dispose();
    }
}
