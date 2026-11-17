using FluentAssertions;
using Infrastructure;
using Xunit;

namespace Framework.Tests.Messaging;

public class AdaptiveIntervalTests
{
    private static AdaptiveInterval Create(
        double minSec = 10,
        double maxSec = 60,
        double failBaseSec = 1,
        double jitter = 0.2)
    {
        return new AdaptiveInterval(TimeSpan.FromSeconds(minSec),
            TimeSpan.FromSeconds(maxSec),
            TimeSpan.FromSeconds(failBaseSec),
            jitter);
    }

    [Fact]
    public void InitialDelay_IsAroundMinInterval()
    {
        var interval = Create(jitter: 0);

        var delay = interval.GetNextDelay();

        delay.TotalSeconds.Should().BeApproximately(10, 0.01);
    }

    [Fact]
    public void ConsecutiveSuccesses_GrowsTowardMaxInterval()
    {
        var interval = Create(jitter: 0);

        for (var i = 0; i < 10; i++)
            interval.RecordSuccess();

        var delay = interval.GetNextDelay();

        delay.TotalSeconds.Should().BeApproximately(60, 0.01);
    }

    [Fact]
    public void ConsecutiveFailures_ExponentialBackoff()
    {
        var interval = Create(jitter: 0);

        interval.RecordFailure();
        var d1 = interval.GetNextDelay();

        interval.RecordFailure();
        var d2 = interval.GetNextDelay();

        interval.RecordFailure();
        var d3 = interval.GetNextDelay();

        d1.TotalSeconds.Should().BeApproximately(1, 0.01);
        d2.TotalSeconds.Should().BeApproximately(2, 0.01);
        d3.TotalSeconds.Should().BeApproximately(4, 0.01);
    }

    [Fact]
    public void ExponentialBackoff_CappedAtMaxInterval()
    {
        var interval = Create(maxSec: 30, jitter: 0);

        for (var i = 0; i < 20; i++)
            interval.RecordFailure();

        var delay = interval.GetNextDelay();

        delay.TotalSeconds.Should().BeLessThanOrEqualTo(30);
    }

    [Fact]
    public void FailureThenSuccess_ResetsBackoff()
    {
        var interval = Create(jitter: 0);

        for (var i = 0; i < 5; i++)
            interval.RecordFailure();

        interval.RecordSuccess();
        var delay = interval.GetNextDelay();

        // After 1 success, should be near min + 1 step
        delay.TotalSeconds.Should().BeLessThan(20);
    }

    [Fact]
    public void SuccessThenFailure_ResetsToFailureBase()
    {
        var interval = Create(jitter: 0);

        for (var i = 0; i < 10; i++)
            interval.RecordSuccess();

        interval.RecordFailure();
        var delay = interval.GetNextDelay();

        delay.TotalSeconds.Should().BeApproximately(1, 0.01);
    }

    [Fact]
    public void Jitter_StaysWithinFactor()
    {
        var interval = Create(minSec: 10, maxSec: 60, jitter: 0.2);

        var delays = new List<double>();

        for (var i = 0; i < 1000; i++)
            delays.Add(interval.GetNextDelay().TotalSeconds);

        // Base is 10 (0 successes), jitter ±20% → range [8, 12]
        delays.Should().AllSatisfy(d => d.Should().BeInRange(7.9, 12.1));
    }

    [Fact]
    public void GradualGrowth_IntermediateValues()
    {
        var interval = Create(minSec: 10, maxSec: 60, jitter: 0);

        // 5 out of 10 steps → halfway
        for (var i = 0; i < 5; i++)
            interval.RecordSuccess();

        var delay = interval.GetNextDelay();

        delay.TotalSeconds.Should().BeApproximately(35, 0.01);
    }

    [Fact]
    public async Task ConcurrentOutcomes_NeverLoseBoth()
    {
        // No outcome in a row gives exactly the min interval: a success counts as a step, a failure gives the base.
        var interval = new AdaptiveInterval(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(1),
            jitterFactor: 0);

        interval.RecordSuccess();

        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
        var lost = false;

        await Task.WhenAll(Task.Run(Successes), Task.Run(Failures));

        lost.Should().BeFalse("after any order of the outcomes one of them must count");

        void Successes()
        {
            while (DateTime.UtcNow < deadline && Volatile.Read(ref lost) == false)
                interval.RecordSuccess();
        }

        void Failures()
        {
            while (DateTime.UtcNow < deadline && Volatile.Read(ref lost) == false)
            {
                interval.RecordFailure();

                if (interval.GetNextDelay() == TimeSpan.FromSeconds(10))
                    Volatile.Write(ref lost, true);
            }
        }
    }
}
