using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Xunit;

namespace Framework.Tests.Messaging;

// A durable queue delivers every message to every subscribed observer. An observer that failed must get the message
// on the retry of the side effect, the ones that got it already must not get it again.
[Collection(nameof(OrleansIntegrationCollection))]
public class DurableQueuePartialDeliveryTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Push_OneObserverFails_RetryDeliversOnlyToIt()
    {
        var orleans = GetSiloService<IOrleans>();
        var queueName = $"partial-{Guid.NewGuid():N}";
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

        // The side effects worker retries a failed Execute; the observers resubscribe in between.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await queue.AddObserver(healthyId, healthyReference);
            await queue.AddObserver(flakyId, flakyReference);

            try
            {
                await sideEffect.Execute(orleans);
                break;
            }
            catch (InvalidOperationException)
            {
                // A partial delivery: retried as the worker would.
            }
        }

        healthy.Should().HaveCount(1, "the observer that got the message must not get it again");
        flaky.Should().HaveCount(1, "the observer that failed must get the message on the retry");
    }
}
