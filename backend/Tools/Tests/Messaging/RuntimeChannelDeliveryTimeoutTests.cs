using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimeChannelDeliveryTimeoutTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Publish_SlowObserver_IsRemoved_And_FastObserver_Continues()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var totalMessages = 20;
        var receivedCount = 0;
        var completion = new TaskCompletionSource();

        // Fast listener
        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, _ => {
            var count = Interlocked.Increment(ref receivedCount);
            if (count >= totalMessages)
                completion.TrySetResult();
        });

        // Slow listeners
        for (var i = 0; i < 3; i++)
        {
            var slowLifetime = new Lifetime();
            await messaging.ListenChannel<TestMessage>(slowLifetime, channelId, async _ => {
                await Task.Delay(TimeSpan.FromSeconds(10));
            });
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < totalMessages; i++)
        {
            await messaging.PublishChannel(channelId, new TestMessage { Sequence = i });
        }
        sw.Stop();

        // Publish must not wait for slow observers
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        receivedCount.Should().Be(totalMessages);
    }
}
