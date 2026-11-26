using FluentAssertions;
using Infrastructure;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Messaging;

[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimeChannelBufferOverflowTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task CatchUp_BufferOverflow_DetectsGap()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);
        var bufferSize = GetSiloService<IRuntimeChannelConfig>().Value.CatchUpBufferSize;
        var totalMessages = bufferSize + 100;

        for (var i = 1; i <= totalMessages; i++)
            await channel.Publish(new TestMessage { Text = $"msg-{i}", Sequence = i });

        // CatchUp from sequence 0 — far behind oldest available
        var result = await channel.CatchUp(0);

        result.GapDetected.Should().BeTrue();
        result.CurrentSequence.Should().Be(totalMessages);
        result.Messages.Should().HaveCount(bufferSize);
    }

    [Fact]
    public async Task CatchUp_AfterOverflow_GapDetectedForStaleSequence()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);
        var bufferSize = GetSiloService<IRuntimeChannelConfig>().Value.CatchUpBufferSize;
        var totalMessages = bufferSize + 50;

        for (var i = 1; i <= totalMessages; i++)
            await channel.Publish(new TestMessage { Text = $"msg-{i}", Sequence = i });

        // lastSeenSequence=5 is far behind oldest in buffer
        var result = await channel.CatchUp(5);

        result.GapDetected.Should().BeTrue();
        result.Messages.Should().NotBeEmpty();
        result.Messages.Count.Should().BeLessThanOrEqualTo(bufferSize);
    }

    [Fact]
    public async Task CatchUp_AfterOverflow_MessagesHaveCorrectSequentialSequences()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);
        var bufferSize = GetSiloService<IRuntimeChannelConfig>().Value.CatchUpBufferSize;
        var totalMessages = bufferSize + 77;

        for (var i = 1; i <= totalMessages; i++)
            await channel.Publish(new TestMessage { Text = $"msg-{i}", Sequence = i });

        var result = await channel.CatchUp(0);

        result.GapDetected.Should().BeTrue();
        result.Messages.Should().HaveCount(bufferSize);

        // Sequences must be strictly sequential with no corruption from modulo wraparound
        var expectedStart = totalMessages - bufferSize + 1;

        for (var i = 0; i < result.Messages.Count; i++)
            result.Messages[i].Sequence.Should().Be(expectedStart + i);
    }

    [Fact]
    public async Task CatchUp_AtExactBoundary_NoGapDetected()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);
        var bufferSize = GetSiloService<IRuntimeChannelConfig>().Value.CatchUpBufferSize;
        var totalMessages = bufferSize + 10;

        for (var i = 1; i <= totalMessages; i++)
            await channel.Publish(new TestMessage { Text = $"msg-{i}", Sequence = i });

        // oldestInBuffer = totalMessages - bufferSize + 1
        // lastSeenSequence = oldestInBuffer - 1 is the exact boundary (no gap)
        var oldestInBuffer = totalMessages - bufferSize + 1;
        var lastSeenAtBoundary = oldestInBuffer - 1;

        var result = await channel.CatchUp(lastSeenAtBoundary);

        result.GapDetected.Should().BeFalse();
        result.Messages.Should().HaveCount(bufferSize);
        result.Messages[0].Sequence.Should().Be(oldestInBuffer);
    }

    [Fact]
    public async Task CatchUp_OneBeforeBoundary_GapDetected()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);
        var bufferSize = GetSiloService<IRuntimeChannelConfig>().Value.CatchUpBufferSize;
        var totalMessages = bufferSize + 10;

        for (var i = 1; i <= totalMessages; i++)
            await channel.Publish(new TestMessage { Text = $"msg-{i}", Sequence = i });

        // lastSeenSequence = oldestInBuffer - 2 means one message before boundary is missing
        var oldestInBuffer = totalMessages - bufferSize + 1;
        var lastSeenOneBeforeBoundary = oldestInBuffer - 2;

        var result = await channel.CatchUp(lastSeenOneBeforeBoundary);

        result.GapDetected.Should().BeTrue();
        result.CurrentSequence.Should().Be(totalMessages);
    }

    // The observer holds the first message: the queue behind it fills up, the next publish drops the observer. What
    // was queued before still comes, what is published after the drop does not.
    [Fact]
    public async Task Publish_ObserverQueueOverflows_ObserverRemovedAfterDrainingItsQueue()
    {
        var channel = GetGrain<IRuntimeChannel>(Guid.NewGuid().ToString());
        var bufferSize = GetSiloService<IRuntimeChannelConfig>().Value.CatchUpBufferSize;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource();
        var received = new List<int>();

        var observer = new RuntimeChannelObserver(async message => {
            entered.TrySetResult();
            await release.Task;

            lock (received)
                received.Add(((TestMessage)message).Sequence);
        });

        await channel.AddObserver(observer.Id, GrainFactory.CreateObjectReference<IRuntimeChannelObserver>(observer));

        // The first message is on its way and its queue is empty: the next bufferSize fill the queue, one more overflows.
        await channel.Publish(new TestMessage { Sequence = 1 });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var delivered = bufferSize + 1;
        var overflowing = delivered + 1;

        for (var i = 2; i <= overflowing; i++)
            await channel.Publish(new TestMessage { Sequence = i });

        await channel.Publish(new TestMessage { Sequence = overflowing + 1 });

        release.SetResult();

        // The queue is delivered in order, so a message queued after the drop would come after the last one of it.
        await TestWait.Until(() => received.LockedCount() == delivered);

        lock (received)
            received.Should().Equal(Enumerable.Range(1, delivered));

        var result = await channel.CatchUp(overflowing);
        result.Messages.Select(message => ((TestMessage)message.Payload).Sequence).Should().Equal(overflowing + 1);
    }
}
