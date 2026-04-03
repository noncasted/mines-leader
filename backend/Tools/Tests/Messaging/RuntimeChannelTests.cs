using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

/// <summary>
/// Tests RuntimeChannel: pub/sub broadcast, multiple subscribers, listener isolation.
/// RuntimeChannel is in-memory (no side effects needed).
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimeChannelTests(OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture) {
    [Fact]
    public async Task Publish_SingleSubscriber_ReceivesMessage() {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(
            new Lifetime(), channelId, msg => received.TrySetResult(msg));

        await messaging.PublishChannel(channelId, new TestMessage { Text = "broadcast", Sequence = 1 });

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Text.Should().Be("broadcast");
        result.Sequence.Should().Be(1);
    }

    [Fact]
    public async Task Publish_MultipleSubscribers_AllReceive() {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received1 = new TaskCompletionSource<TestMessage>();
        var received2 = new TaskCompletionSource<TestMessage>();
        var received3 = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => received1.TrySetResult(msg));
        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => received2.TrySetResult(msg));
        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => received3.TrySetResult(msg));

        await messaging.PublishChannel(channelId, new TestMessage { Text = "all", Sequence = 7 });

        var r1 = await received1.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var r2 = await received2.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var r3 = await received3.Task.WaitAsync(TimeSpan.FromSeconds(5));

        r1.Text.Should().Be("all");
        r2.Text.Should().Be("all");
        r3.Text.Should().Be("all");
    }

    [Fact]
    public async Task Publish_MultipleMessages_AllDeliveredInOrder() {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new List<int>();
        var allReceived = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(
            new Lifetime(), channelId, msg => {
                lock (received) {
                    received.Add(msg.Sequence);
                    if (received.Count >= 10)
                        allReceived.TrySetResult();
                }
            });

        for (var i = 0; i < 10; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Sequence = i });

        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Should().HaveCount(10);
        received.Should().BeEquivalentTo(Enumerable.Range(0, 10));
    }

    [Fact]
    public async Task Publish_DifferentChannels_Isolated() {
        var channelA = new TestChannelId(Guid.NewGuid().ToString());
        var channelB = new TestChannelId(Guid.NewGuid().ToString());
        var receivedA = new List<string>();
        var receivedB = new List<string>();
        var doneA = new TaskCompletionSource();
        var doneB = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelA, msg => {
            lock (receivedA) { receivedA.Add(msg.Text); doneA.TrySetResult(); }
        });
        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelB, msg => {
            lock (receivedB) { receivedB.Add(msg.Text); doneB.TrySetResult(); }
        });

        await messaging.PublishChannel(channelA, new TestMessage { Text = "for-A" });
        await messaging.PublishChannel(channelB, new TestMessage { Text = "for-B" });

        await doneA.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await doneB.Task.WaitAsync(TimeSpan.FromSeconds(5));

        receivedA.Should().ContainSingle().Which.Should().Be("for-A");
        receivedB.Should().ContainSingle().Which.Should().Be("for-B");
    }

    [Fact]
    public async Task Publish_TerminatedListener_NoDelivery() {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new List<string>();
        var lifetime = new Lifetime();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(
            lifetime, channelId, msg => { lock (received) received.Add(msg.Text); });

        // Terminate — unsubscribes
        lifetime.Terminate();

        await messaging.PublishChannel(channelId, new TestMessage { Text = "ghost" });
        await Task.Delay(100);

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_NoSubscribers_DoesNotThrow() {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();

        var act = () => messaging.PublishChannel(channelId, new TestMessage { Text = "void" });

        await act.Should().NotThrowAsync();
    }
}
