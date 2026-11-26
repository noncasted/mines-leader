using FluentAssertions;
using Game.ClientTelemetry;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The timer of a link and the clock it is checked against are different clocks: a timer of the runtime may fire a few
// milliseconds before the wall time of the request plus IdleAfter.
public class ClientTelemetryHttpLinksTests
{
    private static readonly TimeSpan IdleAfter = TimeSpan.FromMilliseconds(300);

    [Fact]
    public void ATimerFiredEarly_ArmsAgain_AndEndsTheLinkAtItsTime()
    {
        var time = new ManualTimeProvider();
        var connections = new RecordingConnections();
        var links = new ClientTelemetryHttpLinks(
            connections,
            new ClientTelemetryHttpOptions { IdleAfter = IdleAfter },
            new ClientTelemetryStorageOptions(),
            time,
            NullLogger<ClientTelemetryHttpLinks>.Instance);

        links.Of(Guid.NewGuid());
        var link = connections.Last!;

        time.Advance(IdleAfter - TimeSpan.FromMilliseconds(2));
        time.Timer.Fire();
        link.IsTerminated.Should().BeFalse();

        time.Timer.DueTime.Should().Be(TimeSpan.FromMilliseconds(2));
        time.Advance(TimeSpan.FromMilliseconds(2));
        time.Timer.Fire();
        link.IsTerminated.Should().BeTrue();
    }

    private sealed class RecordingConnections : IClientTelemetryConnections
    {
        public IReadOnlyLifetime? Last { get; private set; }

        public ClientTelemetryConnection Of(IReadOnlyLifetime connection)
        {
            Last = connection;
            return new ClientTelemetryConnection(null!, null!, NullLogger<ClientTelemetryConnection>.Instance);
        }
    }

    // One timer, fired by the test.
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);

        public ManualTimer Timer { get; private set; } = null!;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Timer = new ManualTimer(callback, state, dueTime);
            return Timer;
        }
    }

    private sealed class ManualTimer : ITimer
    {
        public ManualTimer(TimerCallback callback, object? state, TimeSpan dueTime)
        {
            _callback = callback;
            _state = state;
            DueTime = dueTime;
        }

        private readonly TimerCallback _callback;
        private readonly object? _state;

        public TimeSpan DueTime { get; private set; }

        public void Fire()
        {
            DueTime.Should().NotBe(Timeout.InfiniteTimeSpan, "a timer fires only while armed");
            DueTime = Timeout.InfiniteTimeSpan;
            _callback(_state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueTime = dueTime;
            return true;
        }

        public void Dispose() => DueTime = Timeout.InfiniteTimeSpan;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
