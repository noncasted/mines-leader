using Internal;
using FluentAssertions;
using Infrastructure;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Messaging;

[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimeChannelTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Publish_SingleSubscriber_ReceivesMessage()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(TestLifetime, channelId, msg => received.TrySetResult(msg));

        await messaging.PublishChannel(channelId, new TestMessage { Text = "broadcast", Sequence = 1 });

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.Text.Should().Be("broadcast");
        result.Sequence.Should().Be(1);
    }

    [Fact]
    public async Task Publish_MultipleSubscribers_AllReceive()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received1 = new TaskCompletionSource<TestMessage>();
        var received2 = new TaskCompletionSource<TestMessage>();
        var received3 = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(TestLifetime, channelId, msg => received1.TrySetResult(msg));
        await messaging.ListenChannel<TestMessage>(TestLifetime, channelId, msg => received2.TrySetResult(msg));
        await messaging.ListenChannel<TestMessage>(TestLifetime, channelId, msg => received3.TrySetResult(msg));

        await messaging.PublishChannel(channelId, new TestMessage { Text = "all", Sequence = 7 });

        var r1 = await received1.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var r2 = await received2.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var r3 = await received3.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        r1.Text.Should().Be("all");
        r2.Text.Should().Be("all");
        r3.Text.Should().Be("all");
    }

    [Fact]
    public async Task Publish_AsyncListener_DoesNotBlockPublish()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await messaging.ListenChannel<TestMessage>(TestLifetime, channelId, async _ => {
            await release.Task;
            completed.TrySetResult();
        });

        // The listener cannot finish before the release, so a publish that waited for it would time out here.
        await messaging.PublishChannel(channelId, new TestMessage { Text = "async" })
                       .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        completed.Task.IsCompleted.Should().BeFalse();

        release.TrySetResult();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Publish_MultipleMessages_AllDeliveredInOrder()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new List<int>();
        var allReceived = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(TestLifetime, channelId, msg => {
            lock (received)
            {
                received.Add(msg.Sequence);

                if (received.Count >= 10)
                    allReceived.TrySetResult();
            }
        });

        for (var i = 0; i < 10; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Sequence = i });

        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        received.Should().HaveCount(10);
        received.Should().Equal(Enumerable.Range(0, 10).ToList());
    }

    [Fact]
    public async Task Observer_BufferedLiveDelivery_AfterCatchUpSkipsDuplicate()
    {
        var received = new List<int>();
        var observer = new RuntimeChannelObserver(message => {
            var typed = (TestMessage)message;
            received.Add(typed.Sequence);
            return Task.CompletedTask;
        });

        await observer.Send(new SequencedMessage
        {
            Sequence = 3,
            Payload = new TestMessage { Sequence = 3 }
        });

        observer.BeginBuffering();

        await observer.Send(new SequencedMessage
        {
            Sequence = 5,
            Payload = new TestMessage { Sequence = 5 }
        });

        await observer.ReplayCatchUp([
            new SequencedMessage { Sequence = 4, Payload = new TestMessage { Sequence = 4 } },
            new SequencedMessage { Sequence = 5, Payload = new TestMessage { Sequence = 5 } }
        ]);
        await observer.EndBuffering();

        received.Should().Equal([3, 4, 5]);
    }

    [Fact]
    public async Task Publish_DifferentChannels_Isolated()
    {
        var channelA = new TestChannelId(Guid.NewGuid().ToString());
        var channelB = new TestChannelId(Guid.NewGuid().ToString());
        var receivedA = new List<string>();
        var receivedB = new List<string>();
        var doneA = new TaskCompletionSource();
        var doneB = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(TestLifetime, channelA, msg => {
            lock (receivedA)
            {
                receivedA.Add(msg.Text);
                doneA.TrySetResult();
            }
        });

        await messaging.ListenChannel<TestMessage>(TestLifetime, channelB, msg => {
            lock (receivedB)
            {
                receivedB.Add(msg.Text);
                doneB.TrySetResult();
            }
        });

        await messaging.PublishChannel(channelA, new TestMessage { Text = "for-A" });
        await messaging.PublishChannel(channelB, new TestMessage { Text = "for-B" });

        await doneA.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await doneB.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        receivedA.Should().ContainSingle().Which.Should().Be("for-A");
        receivedB.Should().ContainSingle().Which.Should().Be("for-B");
    }

    [Fact]
    public async Task Publish_TerminatedListener_NoDelivery()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new List<string>();
        var lifetime = TestLifetime.Child();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(lifetime, channelId, msg => {
            lock (received)
                received.Add(msg.Text);
        });

        // Terminate — unsubscribes
        lifetime.Terminate();

        // A window, not a marker: the order holds only per observer, and the terminated listener has no observer left to
        // carry a marker after the ghost.
        await messaging.PublishChannel(channelId, new TestMessage { Text = "ghost" });
        await Task.Delay(100, TestContext.Current.CancellationToken);

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_NoSubscribers_ChannelRemainsFunctional()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();

        // Publish to empty channel — should not throw
        await messaging.PublishChannel(channelId, new TestMessage { Text = "void" });

        // Channel should still work: subscribe and publish a new message
        var received = new TaskCompletionSource<TestMessage>();
        await messaging.ListenChannel<TestMessage>(TestLifetime, channelId, msg => received.TrySetResult(msg));

        await messaging.PublishChannel(channelId, new TestMessage { Text = "after-empty", Sequence = 42 });

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.Text.Should().Be("after-empty");
        result.Sequence.Should().Be(42);
    }

    [Fact]
    public async Task Publish_SubscriberAddedAfterPublish_DoesNotReceiveOldMessage()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();

        // Publish before anyone is listening
        await messaging.PublishChannel(channelId, new TestMessage { Text = "old" });

        // Now subscribe
        var received = new List<string>();

        await messaging.ListenChannel<TestMessage>(TestLifetime, channelId, msg => {
            lock (received)
                received.Add(msg.Text);
        });

        // The order of an observer is kept: a late delivery of the old message would come before the new one.
        await messaging.PublishChannel(channelId, new TestMessage { Text = "new" });
        await TestWait.Until(() => received.LockedCount() >= 1);

        lock (received)
            received.Should().Equal("new");
    }

    [Fact]
    public async Task Publish_ConcurrentRapidFire_AllDelivered()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new List<int>();
        var allReceived = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();
        const int messageCount = 20;

        await messaging.ListenChannel<TestMessage>(TestLifetime, channelId, msg => {
            lock (received)
            {
                received.Add(msg.Sequence);

                if (received.Count >= messageCount)
                    allReceived.TrySetResult();
            }
        });

        // Fire all publishes concurrently
        var tasks = Enumerable.Range(0, messageCount)
                              .Select(i => messaging.PublishChannel(channelId, new TestMessage { Sequence = i }))
                              .ToList();
        await Task.WhenAll(tasks);

        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        received.Should().HaveCount(messageCount);
        received.Should().BeEquivalentTo(Enumerable.Range(0, messageCount));
    }

    [Fact]
    public async Task Publish_PartialTermination_RemainingListenersStillReceive()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var lifetime1 = TestLifetime.Child();
        var received1 = new List<string>();
        var received2 = new List<string>();
        var done2 = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(lifetime1, channelId, msg => {
            lock (received1)
                received1.Add(msg.Text);
        });

        await messaging.ListenChannel<TestMessage>(TestLifetime, channelId, msg => {
            lock (received2)
            {
                received2.Add(msg.Text);
                done2.TrySetResult();
            }
        });

        // Terminate first listener
        lifetime1.Terminate();

        await messaging.PublishChannel(channelId, new TestMessage { Text = "after-terminate" });
        await done2.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        received1.Should().BeEmpty();
        received2.Should().ContainSingle().Which.Should().Be("after-terminate");
    }
}