using Cluster.Discovery;
using Cluster.Monitoring;
using Console;
using Framework.Tests.Fixtures;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Console;

public class SideEffectsAggregatorServiceTests
{
    private static readonly DateTime Second = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AServiceThatDoesNotAnswer_IsListedAsFailed()
    {
        var answering = Service("answering");
        var silent = Service("silent");

        var aggregate = await Aggregate(
            (answering, Answer(answering, new SideEffectsLiveData { QueueCount = 3 })),
            (silent, null));

        Assert.Equal(2, aggregate.Services.Count);
        Assert.Equal(3, aggregate.QueueCount);
        Assert.Contains(aggregate.Services, entry => entry.ServiceId == silent.Id && entry.ServiceTag == "silent");
    }

    // Every service counts the same shared tables: the counters are taken once, only the throughput of each is its own.
    [Fact]
    public async Task SharedCounters_AreTakenOnce_ThroughputIsSummed()
    {
        var first = Service("first");
        var second = Service("second");

        var aggregate = await Aggregate(
            (first, Answer(first, Shared(processed: 2))),
            (second, Answer(second, Shared(processed: 5))));

        Assert.Equal(3, aggregate.QueueCount);
        Assert.Equal(1, aggregate.ProcessingCount);
        Assert.Equal(4, aggregate.RetryCount);
        Assert.Equal(2, aggregate.DeadLetterCount);
        Assert.Equal(7, Assert.Single(aggregate.AggregatedHistory).Processed);
        Assert.Equal(0, aggregate.MissingServices);
        Assert.True(aggregate.HasCounts);
    }

    [Fact]
    public async Task AServiceThatDoesNotAnswer_MarksTheSumPartial()
    {
        var answering = Service("answering");
        var silent = Service("silent");

        var aggregate = await Aggregate((answering, Answer(answering, Shared(processed: 2))), (silent, null));

        Assert.Equal(1, aggregate.MissingServices);
        Assert.True(aggregate.HasCounts);
    }

    [Fact]
    public async Task NoServiceAnswers_TheCountersAreUnknown()
    {
        var silent = Service("silent");

        var aggregate = await Aggregate((silent, null));

        Assert.Equal(1, aggregate.MissingServices);
        Assert.False(aggregate.HasCounts);
    }

    private static SideEffectsLiveData Shared(long processed)
    {
        return new SideEffectsLiveData
        {
            QueueCount = 3,
            ProcessingCount = 1,
            RetryCount = 4,
            DeadLetterCount = 2,
            ThroughputHistory = [new SideEffectsThroughputEntry { Timestamp = Second, Processed = processed }]
        };
    }

    private static SideEffectsSnapshotResponse Answer(ServiceOverview service, SideEffectsLiveData data)
    {
        return new SideEffectsSnapshotResponse { ServiceTag = service.Tag.ToString(), ServiceId = service.Id, Data = data };
    }

    // A null answer: the request of that service throws.
    private static async Task<SideEffectsAggregate> Aggregate(params (ServiceOverview Service, SideEffectsSnapshotResponse? Answer)[] services)
    {
        var discovery = Substitute.For<IServiceDiscovery>();
        discovery.Entries.Returns(services.ToDictionary(entry => entry.Service.Id, entry => (IServiceOverview)entry.Service));

        var messaging = Substitute.For<IMessaging>();

        foreach (var (service, answer) in services)
        {
            var call = messaging.RuntimePipe.Send<SideEffectsSnapshotResponse>(
                Arg.Is<IRuntimePipeId>(id => id.ToRaw().Contains(service.Id.ToString())), Arg.Any<object>());

            if (answer == null)
                call.Returns<Task<SideEffectsSnapshotResponse>>(_ => throw new TimeoutException("no answer"));
            else
                call.Returns(answer);
        }

        var aggregator = new SideEffectsAggregatorService(messaging, discovery, NullLogger<SideEffectsAggregatorService>.Instance);

        await aggregator.StartAsync(TestContext.Current.CancellationToken);
        await TestWait.Until(() => aggregator.Aggregate.Value.UpdatedAt != default);
        await aggregator.StopAsync(TestContext.Current.CancellationToken);

        return aggregator.Aggregate.Value;
    }

    private static ServiceOverview Service(string tag)
    {
        return new ServiceOverview { Id = Guid.NewGuid(), Tag = new ServiceTag(tag), UpdateTime = DateTime.UtcNow };
    }
}
