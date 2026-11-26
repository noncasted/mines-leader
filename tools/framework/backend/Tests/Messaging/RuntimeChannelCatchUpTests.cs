using System.Diagnostics;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Messaging;

[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimeChannelCatchUpTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task CatchUp_NoMissedMessages_ReturnsEmpty()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        await channel.Publish(new TestMessage { Text = "a", Sequence = 1 });
        await channel.Publish(new TestMessage { Text = "b", Sequence = 2 });

        var result = await channel.CatchUp(2);

        result.Messages.Should().BeEmpty();
        result.GapDetected.Should().BeFalse();
        result.CurrentSequence.Should().Be(2);
    }

    [Fact]
    public async Task CatchUp_MissedMessages_ReplaysFromBuffer()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        await channel.Publish(new TestMessage { Text = "a", Sequence = 1 });
        await channel.Publish(new TestMessage { Text = "b", Sequence = 2 });
        await channel.Publish(new TestMessage { Text = "c", Sequence = 3 });

        var result = await channel.CatchUp(1);

        result.Messages.Should().HaveCount(2);
        result.Messages[0].Sequence.Should().Be(2);
        result.Messages[1].Sequence.Should().Be(3);
        result.GapDetected.Should().BeFalse();
        result.CurrentSequence.Should().Be(3);
    }

    [Fact]
    public async Task CatchUp_FromZero_ReplaysAll()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        await channel.Publish(new TestMessage { Text = "a", Sequence = 1 });
        await channel.Publish(new TestMessage { Text = "b", Sequence = 2 });

        var result = await channel.CatchUp(0);

        result.Messages.Should().HaveCount(2);
        result.Messages[0].Sequence.Should().Be(1);
        result.Messages[1].Sequence.Should().Be(2);
        result.GapDetected.Should().BeFalse();
    }

    [Fact]
    public async Task CatchUp_EmptyChannel_ReturnsEmpty()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        var result = await channel.CatchUp(0);

        result.Messages.Should().BeEmpty();
        result.GapDetected.Should().BeFalse();
        result.CurrentSequence.Should().Be(0);
    }

    [Fact]
    public async Task CatchUp_LastSeenGreaterThanCurrentSequence_DetectsGap()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        var result = await channel.CatchUp(5);

        result.Messages.Should().BeEmpty();
        result.GapDetected.Should().BeTrue();
        result.CurrentSequence.Should().Be(0);
    }

    [Fact]
    public async Task CatchUp_PayloadPreserved()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        await channel.Publish(new TestMessage { Text = "hello", Sequence = 42 });

        var result = await channel.CatchUp(0);

        result.Messages.Should().HaveCount(1);
        var payload = result.Messages[0].Payload.Should().BeOfType<TestMessage>().Subject;
        payload.Text.Should().Be("hello");
        payload.Sequence.Should().Be(42);
    }

    [Fact]
    public async Task Publish_SequenceNumbers_MonotonicallyIncreasing()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        for (var i = 0; i < 20; i++)
            await channel.Publish(new TestMessage { Sequence = i });

        var result = await channel.CatchUp(0);

        result.Messages.Should().HaveCount(20);

        for (var i = 0; i < 20; i++)
            result.Messages[i].Sequence.Should().Be(i + 1);
    }

    [Fact]
    public async Task Publish_ObserverReceivesUnwrappedPayload()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(TestLifetime, channelId,
            msg => received.TrySetResult(msg));

        await messaging.PublishChannel(channelId, new TestMessage { Text = "unwrapped", Sequence = 7 });

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.Text.Should().Be("unwrapped");
        result.Sequence.Should().Be(7);
    }

    // A client of its own with short intervals: the resubscribe loop of the silo client sleeps the shortest interval
    // of all its listeners (up to a minute once they are stable).
    [Fact]
    public async Task CatchUp_AfterResubscribe_ReplaysFromLastSeen()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var client = CreateClient();
        var received = new List<int>();
        var failedOnce = 0;
        var lifetime = TestLifetime.Child();

        await client.Listen<TestMessage>(lifetime, channelId, message => {
            if (message.Sequence == 3 && Interlocked.Exchange(ref failedOnce, 1) == 0)
                throw new InvalidOperationException("the channel removes the observer of a failed send");

            lock (received)
                received.Add(message.Sequence);

            return Task.CompletedTask;
        });

        // 1 and 2 arrive; 3 fails, the channel removes the observer and drops what is queued for it.
        for (var i = 1; i <= 6; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Sequence = i });

        await TestWait.Until(() => Volatile.Read(ref failedOnce) == 1);

        lock (received)
            received.Should().Equal(1, 2);

        // The first pass of the loop binds the observer again and replays from the last seen sequence.
        await client.Start(lifetime);
        await TestWait.Until(() => received.LockedCount() >= 6);

        // The listener is live again after the resubscribe, the replayed messages come once.
        await messaging.PublishChannel(channelId, new TestMessage { Sequence = 7 });
        await TestWait.Until(() => received.LockedCount() >= 7);

        lock (received)
            received.Should().Equal(1, 2, 3, 4, 5, 6, 7);

        lifetime.Terminate();
    }

    [Fact]
    public async Task ResubscribeLoop_RunsWithTheIntervalsOfTheOptions()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var client = CreateClient();
        var seen = 0;
        var attempts = 0;
        var lifetime = TestLifetime.Child();

        // Before the first listener: the loop sleeps the interval of the options with no listeners too.
        var stopwatch = Stopwatch.StartNew();
        await client.Start(lifetime);

        await client.Listen<TestMessage>(lifetime, channelId, message => {
            if (message.Sequence == 1)
            {
                Volatile.Write(ref seen, 1);
                return Task.CompletedTask;
            }

            if (Interlocked.Increment(ref attempts) == 1)
                throw new InvalidOperationException("the channel removes the observer of a failed send");

            return Task.CompletedTask;
        });

        await messaging.PublishChannel(channelId, new TestMessage { Sequence = 1 });
        await TestWait.Until(() => Volatile.Read(ref seen) == 1);

        await messaging.PublishChannel(channelId, new TestMessage { Sequence = 2 });
        await TestWait.Until(() => Volatile.Read(ref attempts) >= 1);

        // Only a later pass of the loop brings the message again. With the default interval the second pass comes
        // 10 s after the start.
        await TestWait.Until(() => Volatile.Read(ref attempts) >= 2);

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(8));
        lifetime.Terminate();
    }

    [Fact]
    public async Task Listen_AfterPreviousListenerTerminated_NewListenerReceives()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var first = new List<int>();
        var second = new List<int>();
        var firstLifetime = TestLifetime.Child();
        var secondLifetime = TestLifetime.Child();

        await messaging.ListenChannel<TestMessage>(firstLifetime, channelId, message => {
            lock (first)
                first.Add(message.Sequence);
        });

        for (var i = 1; i <= 3; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Sequence = i });

        await TestWait.Until(() => first.LockedCount() == 3);
        firstLifetime.Terminate();

        await messaging.ListenChannel<TestMessage>(secondLifetime, channelId, message => {
            lock (second)
                second.Add(message.Sequence);
        });

        await messaging.PublishChannel(channelId, new TestMessage { Sequence = 4 });
        await TestWait.Until(() => second.LockedCount() == 1);

        lock (second)
            second.Should().Equal(4);

        first.LockedCount().Should().Be(3, "a terminated listener gets nothing more");
        secondLifetime.Terminate();
    }

    private RuntimeChannelClient CreateClient()
    {
        var config = Substitute.For<IRuntimeChannelConfig>();

        config.Value.Returns(new RuntimeChannelOptions
        {
            ResubscribeMinIntervalMs = 50,
            ResubscribeMaxIntervalMs = 50
        });

        var services = new ServiceCollection().AddSingleton(config).BuildServiceProvider();
        return new RuntimeChannelClient(GetSiloService<IOrleans>(), services, NullLogger<RuntimeChannelClient>.Instance);
    }
}
