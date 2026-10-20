using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

/// <summary>
/// F2.39: DurableQueue and RuntimePipe keep their in-memory observers across idle activation collection.
/// The keep-alive is requested when the observer is added: DelayDeactivation from OnDeactivateAsync
/// does not stop a deactivation that has already started.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class MessagingKeepAliveTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task DurableQueue_IdleCollection_KeepsObserver()
    {
        var queue = GetGrain<IDurableQueue>($"keep-alive-{Guid.NewGuid():N}");
        var received = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new DurableQueueObserver(message => received.TrySetResult(message));
        var reference = GrainFactory.CreateObjectReference<IDurableQueueObserver>(observer);

        await queue.AddObserver(Guid.NewGuid(), reference);
        await CollectIdleActivations();

        await queue.Push(new TestMessage { Text = "after collection", Sequence = 1 });

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        message.Should().BeOfType<TestMessage>().Which.Text.Should().Be("after collection");

        GC.KeepAlive(observer);
    }

    [Fact]
    public async Task RuntimePipe_IdleCollection_KeepsObserver()
    {
        var pipe = GetGrain<IRuntimePipe>($"keep-alive-{Guid.NewGuid():N}");
        var observer = new RuntimePipeObserver(NullLogger.Instance);
        observer.BindResponseHandler(message => Task.FromResult(message));
        var reference = GrainFactory.CreateObjectReference<IRuntimePipeObserver>(observer);

        await pipe.BindObserver(Guid.NewGuid(), reference);
        await CollectIdleActivations();

        (await pipe.HasObserver()).Should().BeTrue("the bound observer must survive idle collection");

        GC.KeepAlive(observer);
    }

    private async Task CollectIdleActivations()
    {
        // Let the activations become idle, then collect everything idle for longer than that.
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        await GrainFactory.GetGrain<IManagementGrain>(0).ForceActivationCollection(TimeSpan.FromMilliseconds(100));
    }
}
