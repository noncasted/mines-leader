using Internal;

namespace Game.ClientTelemetry;

// One link of a client over HTTP: the lifetime stands for a socket, the timer ends it after a silence. Touched under the lock of
// ClientTelemetryHttpLinks.
internal sealed class ClientTelemetryHttpLink
{
    public ClientTelemetryHttpLink(Guid id, TimeProvider time, Action<ClientTelemetryHttpLink> expire)
    {
        Id = id;
        LastRequest = time.GetUtcNow();
        _timer = time.CreateTimer(_ => expire(this), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private readonly ITimer _timer;

    public Guid Id { get; }
    public Lifetime Lifetime { get; } = new();
    public DateTimeOffset LastRequest { get; private set; }

    public void Touch(DateTimeOffset now, TimeSpan idleAfter)
    {
        LastRequest = now;
        _timer.Change(idleAfter, Timeout.InfiniteTimeSpan);
    }

    public void End()
    {
        _timer.Dispose();
        Lifetime.Terminate();
    }
}
