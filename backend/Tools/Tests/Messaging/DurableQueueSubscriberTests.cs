using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

/// <summary>
/// Tests DurableQueue subscriber failure handling and no-subscriber behavior.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class DurableQueueSubscriberTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Push_NoSubscribers_Throws()
    {
        var queueId = Guid.NewGuid().ToString();
        var queue = GetGrain<IDurableQueue>(queueId);

        // Push to empty queue should throw so the message stays in processing for requeue
        var act = () => queue.Push(new TestMessage { Text = "no-one-listening" });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Push_NoSubscribers_SubsequentSubscriberWorks()
    {
        var queueId = Guid.NewGuid().ToString();
        var queue = GetGrain<IDurableQueue>(queueId);

        // Push to empty queue throws
        var act = () => queue.Push(new TestMessage { Text = "lost" });
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Add subscriber, queue should work
        var observer = new DurableQueueObserver(_ => {
        });
        var observerRef = GrainFactory.CreateObjectReference<IDurableQueueObserver>(observer);
        await queue.AddObserver(Guid.NewGuid(), observerRef);

        var pushAct = () => queue.Push(new TestMessage { Text = "delivered" });
        await pushAct.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Push_AllSubscribersFail_ThrowsAndRemovesFailedObservers()
    {
        var queueId = Guid.NewGuid().ToString();
        var queue = GetGrain<IDurableQueue>(queueId);
        var observer = new DurableQueueObserver(_ => {
            throw new InvalidOperationException("subscriber failed");
        });
        var observerRef = GrainFactory.CreateObjectReference<IDurableQueueObserver>(observer);

        await queue.AddObserver(Guid.NewGuid(), observerRef);

        var act = () => queue.Push(new TestMessage { Text = "should-retry" });
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*No subscribers successfully processed*");

        var subsequentAct = () => queue.Push(new TestMessage { Text = "still-no-subscriber" });
        await subsequentAct.Should().ThrowAsync<InvalidOperationException>()
                           .WithMessage("*No active subscribers*");
    }
}