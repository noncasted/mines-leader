using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Xunit;

namespace Framework.Tests.Messaging;

// The observer whose delivery failed is removed from the queue; a retry that comes before it resubscribes must not
// complete the message, otherwise that observer never gets it.
[Collection(nameof(OrleansIntegrationCollection))]
public class DurableQueueRetryBeforeResubscribeTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Retry_FailedObserverNotBackYet_Throws()
    {
        var orleans = GetSiloService<IOrleans>();
        var queueName = $"retry-early-{Guid.NewGuid():N}";
        var queue = orleans.GetGrain<IDurableQueue>(queueName);

        var healthy = new List<object>();
        var flaky = new List<object>();
        var flakyCalls = 0;

        var healthyObserver = new DurableQueueObserver(message => healthy.Add(message));

        var flakyObserver = new DurableQueueObserver(message => {
            if (Interlocked.Increment(ref flakyCalls) == 1)
                throw new Exception("observer busy");

            flaky.Add(message);
        });

        var healthyId = Guid.NewGuid();
        var flakyId = Guid.NewGuid();
        var healthyReference = orleans.Client.CreateObjectReference<IDurableQueueObserver>(healthyObserver);
        var flakyReference = orleans.Client.CreateObjectReference<IDurableQueueObserver>(flakyObserver);

        var sideEffect = new DurableQueueSideEffect
        {
            QueueName = queueName,
            Message = new TestMessage { Text = "once" },
            CorrelationId = Guid.NewGuid()
        };

        await queue.AddObserver(healthyId, healthyReference);
        await queue.AddObserver(flakyId, flakyReference);

        var first = () => sideEffect.Execute(orleans);
        await first.Should().ThrowAsync<InvalidOperationException>();

        // Only the healthy observer is subscribed now: the retry must still fail.
        var early = () => sideEffect.Execute(orleans);
        await early.Should().ThrowAsync<InvalidOperationException>("the failed observer has not got the message");

        await queue.AddObserver(flakyId, flakyReference);
        await sideEffect.Execute(orleans);

        healthy.Should().HaveCount(1, "the observer that got the message must not get it again");
        flaky.Should().HaveCount(1, "the observer that failed must get the message once it resubscribes");
    }

    [Fact]
    public async Task Retry_FailedObserverRemovedItself_Completes()
    {
        var orleans = GetSiloService<IOrleans>();
        var queueName = $"retry-removed-{Guid.NewGuid():N}";
        var queue = orleans.GetGrain<IDurableQueue>(queueName);

        var healthy = new List<object>();
        var healthyObserver = new DurableQueueObserver(message => healthy.Add(message));
        var failingObserver = new DurableQueueObserver(_ => throw new Exception("observer busy"));

        var healthyId = Guid.NewGuid();
        var failingId = Guid.NewGuid();

        await queue.AddObserver(healthyId, orleans.Client.CreateObjectReference<IDurableQueueObserver>(healthyObserver));
        await queue.AddObserver(failingId, orleans.Client.CreateObjectReference<IDurableQueueObserver>(failingObserver));

        var sideEffect = new DurableQueueSideEffect
        {
            QueueName = queueName,
            Message = new TestMessage { Text = "once" },
            CorrelationId = Guid.NewGuid()
        };

        var first = () => sideEffect.Execute(orleans);
        await first.Should().ThrowAsync<InvalidOperationException>();

        // An observer that unsubscribed on purpose is not waited for.
        await queue.RemoveObserver(failingId);
        await sideEffect.Execute(orleans);

        healthy.Should().HaveCount(1);
    }
}
