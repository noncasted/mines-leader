using Common.Network;
using Internal;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;

namespace Framework.Tests.Session;

public class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = new();
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private int _createdTimers;

    public int CreatedTimers
    {
        get
        {
            lock (_timers)
                return _createdTimers;
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_timers)
            return _now;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_timers)
        {
            var timer = new ManualTimer(callback, state, _now + dueTime);
            _timers.Add(timer);
            _createdTimers++;
            return timer;
        }
    }

    // Fires the timers that are due, in the order of their due time.
    public void Advance(TimeSpan delta)
    {
        List<ManualTimer> due;

        lock (_timers)
        {
            _now += delta;
            due = _timers.Where(timer => timer.DueAt <= _now).OrderBy(timer => timer.DueAt).ToList();
            _timers.RemoveAll(timer => timer.DueAt <= _now);
        }

        foreach (var timer in due)
            timer.Fire();
    }
}
