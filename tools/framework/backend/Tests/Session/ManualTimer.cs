using Common.Network;
using Internal;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;

namespace Framework.Tests.Session;

public class ManualTimer : ITimer
{
    public ManualTimer(TimerCallback callback, object? state, DateTimeOffset dueAt)
    {
        _callback = callback;
        _state = state;
        DueAt = dueAt;
    }

    private readonly TimerCallback _callback;
    private readonly object? _state;
    private bool _isDisposed;

    public DateTimeOffset DueAt { get; }

    public void Fire()
    {
        if (_isDisposed == false)
            _callback(_state);
    }

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        throw new NotSupportedException();
    }

    public void Dispose()
    {
        _isDisposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
