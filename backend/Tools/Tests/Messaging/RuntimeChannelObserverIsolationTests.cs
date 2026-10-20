using System.Diagnostics;
using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimeChannelObserverIsolationTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task SlowObserver_DoesNotDelayFastObserver()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var deliveryTimeout = TimeSpan.FromSeconds(GetSiloService<IRuntimeChannelConfig>().Value.DeliveryTimeoutSeconds);
        const int totalMessages = 20;
        var received = 0;
        var completion = new TaskCompletionSource();
        var lifetime = new Lifetime();

        await messaging.ListenChannel<TestMessage>(lifetime, channelId, _ => {
            if (Interlocked.Increment(ref received) == totalMessages)
                completion.TrySetResult();
        });

        // Blocks until the test ends: every Send to it hits the delivery timeout.
        var release = new TaskCompletionSource();
        await messaging.ListenChannel<TestMessage>(lifetime, channelId, (TestMessage _) => release.Task);

        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < totalMessages; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Sequence = i });

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Before per-observer queues the first message waited for the slow observer's timeout.
        stopwatch.Elapsed.Should().BeLessThan(deliveryTimeout / 2);

        release.TrySetResult();
        lifetime.Terminate();
    }

    [Fact]
    public async Task AttachedObserver_KeepsChannelActivation_OnCollection()
    {
        var channelName = $"keep-alive-{Guid.NewGuid():N}";
        var channel = GetGrain<IRuntimeChannel>(channelName);
        var observer = new RuntimeChannelObserver(_ => { });
        var reference = Cluster.Client.CreateObjectReference<IRuntimeChannelObserver>(observer);

        await channel.AddObserver(observer.Id, reference);
        await channel.Publish(new TestMessage { Sequence = 1 });

        // Past the keep-alive of the publish itself: only the attached observer holds the activation.
        await WaitPastDeliveryTimeout();
        await ForceCollection();

        // A recreated activation would start its sequence from zero.
        var result = await channel.CatchUp(0);
        result.CurrentSequence.Should().Be(1);

        await channel.RemoveObserver(observer.Id);
        Cluster.Client.DeleteObjectReference<IRuntimeChannelObserver>(reference);
    }

    [Fact]
    public async Task ChannelWithoutObservers_IsCollected()
    {
        var channel = GetGrain<IRuntimeChannel>($"no-observers-{Guid.NewGuid():N}");

        await channel.Publish(new TestMessage { Sequence = 1 });

        // Publish keeps the activation only for the delivery timeout.
        await WaitPastDeliveryTimeout();
        await ForceCollection();

        var result = await channel.CatchUp(0);
        result.CurrentSequence.Should().Be(0);
    }

    private Task WaitPastDeliveryTimeout()
    {
        var deliveryTimeout = TimeSpan.FromSeconds(GetSiloService<IRuntimeChannelConfig>().Value.DeliveryTimeoutSeconds);
        return Task.Delay(deliveryTimeout + TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
    }

    private async Task ForceCollection()
    {
        var management = GrainFactory.GetGrain<IManagementGrain>(0);
        await management.ForceActivationCollection(TimeSpan.Zero);
    }
}
