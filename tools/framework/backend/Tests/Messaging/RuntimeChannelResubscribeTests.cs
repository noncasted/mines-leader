using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Orleans.Core.Internal;
using Xunit;

namespace Framework.Tests.Messaging;

// Resubscribe of a channel listener: two of them in flight together (the first Listen and the resubscribe loop), and
// a channel grain reactivated between two of them (its sequence numbers start again from 1).
[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimeChannelResubscribeTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Resubscribe_Overlapping_BufferedMessageDelivered()
    {
        var channel = Substitute.For<IRuntimeChannel>();
        var firstAdd = new TaskCompletionSource<ChannelPosition>();
        var addCalls = 0;

        channel.AddObserver(default, default!)
               .ReturnsForAnyArgs(_ => Interlocked.Increment(ref addCalls) == 1 ? firstAdd.Task : Task.FromResult(new ChannelPosition()));

        channel.CatchUp(default).ReturnsForAnyArgs(new CatchUpResult { CurrentSequence = 1 });

        var listener = new RuntimeChannelClient.Listener(new RuntimeChannelId("overlap"), channel,
            Substitute.For<IOrleans>(), NullLogger.Instance, new RuntimeChannelOptions());

        var received = new List<object>();

        listener.AddHandler(message => {
            received.Add(message);
            return Task.CompletedTask;
        }, null);

        // The first resubscribe buffers while it binds; a message comes; a second resubscribe starts meanwhile.
        var first = listener.Resubscribe();
        await listener.ObserverSource.Send(new SequencedMessage { Sequence = 1, Payload = "a" });
        var second = listener.Resubscribe();

        firstAdd.SetResult(new ChannelPosition());
        await Task.WhenAll(first, second);

        received.Should().Equal("a");
    }

    // A listener of its own, resubscribed by the test: the resubscribe loop of the silo client sleeps the shortest
    // interval of all its listeners (up to a minute once they are stable), which made a wait for it flaky in the
    // full suite.
    [Fact]
    public async Task Resubscribe_ChannelReactivated_NewMessagesNotDropped()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var orleans = GetSiloService<IOrleans>();
        var channel = GetGrain<IRuntimeChannel>(channelId.ToRaw());
        var listener = new RuntimeChannelClient.Listener(channelId, channel, orleans, NullLogger.Instance,
            new RuntimeChannelOptions());
        var received = new List<string>();

        listener.AddHandler(message => {
            lock (received)
                received.Add(((TestMessage)message).Text);

            return Task.CompletedTask;
        }, null);

        await listener.Resubscribe();

        for (var i = 1; i <= 3; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Text = $"old-{i}" });

        await TestWait.Until(() => received.LockedCount() == 3);

        // A new activation numbers its messages from 1 again: the listener has seen up to 3.
        await channel.Cast<IGrainManagementExtension>().DeactivateOnIdle();
        await TestWait.Until(async () => (await channel.CatchUp(0)).CurrentSequence == 0);

        for (var i = 1; i <= 3; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Text = $"new-{i}" });

        await listener.Resubscribe();

        await TestWait.Until(() => received.LockedCount() == 6);

        lock (received)
            received.Should().Equal("old-1", "old-2", "old-3", "new-1", "new-2", "new-3");

        listener.Cleanup();
    }

    [Fact]
    public async Task Resubscribe_FirstDeliveryOfTheHandlerFailed_ItIsReplayed()
    {
        var (channelId, _, listener) = CreateListener();
        var received = new List<string>();
        var attempts = 0;

        listener.AddHandler(message => {
            if (Interlocked.Increment(ref attempts) == 1)
                throw new InvalidOperationException("the first delivery fails");

            lock (received)
                received.Add(((TestMessage)message).Text);

            return Task.CompletedTask;
        }, null);

        await listener.Resubscribe();
        await GetSiloService<IMessaging>().PublishChannel(channelId, new TestMessage { Text = "first" });

        await TestWait.Until(() => Volatile.Read(ref attempts) == 1);

        await listener.Resubscribe();

        lock (received)
            received.Should().Equal("first");

        listener.Cleanup();
    }

    [Fact]
    public async Task Resubscribe_DroppedBeforeTheFirstMessage_MessageIsCaughtUp()
    {
        var (channelId, channel, listener) = CreateListener();
        var received = new List<string>();

        listener.AddHandler(message => {
            lock (received)
                received.Add(((TestMessage)message).Text);

            return Task.CompletedTask;
        }, null);

        await listener.Resubscribe();

        // What the channel does after a send that did not reach the client.
        await channel.RemoveObserver(listener.ObserverSource.Id);
        await GetSiloService<IMessaging>().PublishChannel(channelId, new TestMessage { Text = "first" });

        await listener.Resubscribe();

        lock (received)
            received.Should().Equal("first");

        listener.Cleanup();
    }

    [Fact]
    public async Task Resubscribe_LateSubscriber_GetsNoHistory()
    {
        var (channelId, _, listener) = CreateListener();
        var received = new List<string>();
        var gaps = 0;

        for (var i = 1; i <= 3; i++)
            await GetSiloService<IMessaging>().PublishChannel(channelId, new TestMessage { Text = $"history-{i}" });

        listener.AddHandler(message => {
            lock (received)
                received.Add(((TestMessage)message).Text);

            return Task.CompletedTask;
        }, () => gaps++);

        await listener.Resubscribe();
        await listener.Resubscribe();

        gaps.Should().Be(0);

        await GetSiloService<IMessaging>().PublishChannel(channelId, new TestMessage { Text = "new" });
        await TestWait.Until(() => received.LockedCount() == 1);

        lock (received)
            received.Should().Equal("new");

        listener.Cleanup();
    }

    [Fact]
    public async Task Delivered_LeavesNoTimerOfTheDeliveryTimeout()
    {
        const int messages = 1_000;

        var (channelId, _, listener) = CreateListener();
        var received = 0;

        listener.AddHandler(_ => {
            Interlocked.Increment(ref received);
            return Task.CompletedTask;
        }, null);

        await listener.Resubscribe();

        var messaging = GetSiloService<IMessaging>();
        var timersBefore = Timer.ActiveCount;

        // Faster than the 5 s of the timeout: every timer left by a delivery is still alive at the end.
        for (var i = 0; i < messages; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Sequence = i });

        await TestWait.Until(() => Volatile.Read(ref received) == messages);

        (Timer.ActiveCount - timersBefore).Should().BeLessThan(messages / 2);

        listener.Cleanup();
    }

    // The handler fails on a message of the catch-up: the rest of the catch-up is dropped, so nothing after it may be
    // taken before the next catch-up replays it, or the last seen sequence skips the dropped ones without a gap.
    [Fact]
    public async Task CatchUpHandlerFailed_LaterMessage_DroppedMessagesReplayed()
    {
        var (listener, published, received) = CreateFailingOnce("b");
        var epoch = Guid.NewGuid();

        await listener.Resubscribe();
        await listener.ObserverSource.Send(Publish(published, epoch, "a"));
        Publish(published, epoch, "b");
        Publish(published, epoch, "c");

        var failedCatchUp = await Record.ExceptionAsync(() => listener.Resubscribe());
        failedCatchUp.Should().BeOfType<AggregateException>("the only handler failed");

        await Record.ExceptionAsync(() => listener.ObserverSource.Send(Publish(published, epoch, "d")));
        await listener.Resubscribe();

        received.Should().Equal("a", "b", "c", "d");
    }

    [Fact]
    public async Task CatchUpHandlerFailed_BufferedMessage_DroppedMessagesReplayed()
    {
        var (listener, published, received) = CreateFailingOnce("b");
        var epoch = Guid.NewGuid();

        await listener.Resubscribe();
        await listener.ObserverSource.Send(Publish(published, epoch, "a"));
        Publish(published, epoch, "b");
        Publish(published, epoch, "c");

        // A message of the channel comes while the resubscribe binds: it waits in the buffer of the observer.
        var bind = new TaskCompletionSource<ChannelPosition>();
        listener.Channel.AddObserver(default, default!).ReturnsForAnyArgs(bind.Task);
        var failing = listener.Resubscribe();
        await listener.ObserverSource.Send(Publish(published, epoch, "d"));
        bind.SetResult(new ChannelPosition { Epoch = epoch });

        (await Record.ExceptionAsync(() => failing)).Should().BeOfType<AggregateException>("the only handler failed");

        listener.Channel.AddObserver(default, default!).ReturnsForAnyArgs(new ChannelPosition { Epoch = epoch });
        await listener.Resubscribe();

        received.Should().Equal("a", "b", "c", "d");
    }

    private static (RuntimeChannelClient.Listener Listener, List<SequencedMessage> Published, List<string> Received)
        CreateFailingOnce(string failing)
    {
        var published = new List<SequencedMessage>();
        var received = new List<string>();
        var channel = Substitute.For<IRuntimeChannel>();

        channel.AddObserver(default, default!).ReturnsForAnyArgs(_ => new ChannelPosition
        {
            Epoch = published.Count == 0 ? Guid.Empty : published[0].Epoch,
            Sequence = published.Count
        });

        channel.CatchUp(default).ReturnsForAnyArgs(call => new CatchUpResult
        {
            Messages = published.Where(m => m.Sequence > call.Arg<long>()).ToArray(),
            CurrentSequence = published.Count,
            Epoch = published[0].Epoch
        });

        var listener = new RuntimeChannelClient.Listener(new RuntimeChannelId("catch-up-failure"), channel,
            Substitute.For<IOrleans>(), NullLogger.Instance, new RuntimeChannelOptions());
        var failed = false;

        listener.AddHandler(message => {
            if ((string)message == failing && failed == false)
            {
                failed = true;
                throw new InvalidOperationException($"the first delivery of {failing} fails");
            }

            received.Add((string)message);
            return Task.CompletedTask;
        }, null);

        return (listener, published, received);
    }

    private static SequencedMessage Publish(List<SequencedMessage> published, Guid epoch, string payload)
    {
        var message = new SequencedMessage { Sequence = published.Count + 1, Payload = payload, Epoch = epoch };
        published.Add(message);
        return message;
    }

    private (TestChannelId Id, IRuntimeChannel Channel, RuntimeChannelClient.Listener Listener) CreateListener()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var channel = GetGrain<IRuntimeChannel>(channelId.ToRaw());

        return (channelId, channel, new RuntimeChannelClient.Listener(channelId, channel,
            GetSiloService<IOrleans>(), NullLogger.Instance, new RuntimeChannelOptions()));
    }
}
