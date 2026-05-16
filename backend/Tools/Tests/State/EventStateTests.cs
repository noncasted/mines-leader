using FluentAssertions;
using Infrastructure;
using Marten;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests event-sourced state (EventState<T>) and its integration with the transaction system.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class EventStateTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    // --- Standalone (non-transactional) event append ---

    [Fact]
    public async Task DirectMarten_SmokeTest()
    {
        var store = GetSiloService<IDocumentStore>();
        using var session = store.QuerySession();
    }

    [Fact]
    public async Task StandaloneAppend_EventsWrittenToMarten()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IEventTestGrain>(id);

        await grain.AppendEventsStandalone(5, "hello");

        var streamId = await grain.GetStreamId();
        var events = await FetchStreamEvents(streamId);

        events.Should().HaveCount(2);
        events[0].Should().BeOfType<CounterIncremented>().Which.Amount.Should().Be(5);
        events[1].Should().BeOfType<LabelChanged>().Which.Label.Should().Be("hello");
    }

    [Fact]
    public async Task StandaloneAppend_AggregateCounterUpdated()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IEventTestGrain>(id);

        await grain.AppendEventsStandalone(5, "hello");

        var counter = await grain.GetCounter();
        counter.Should().Be(5);
    }

    // --- Transactional event append ---

    [Fact]
    public async Task TransactionalAppend_EventsCommitted()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IEventTestGrain>(id);

        await RunTransaction(() => grain.AppendEvents(10, "tx-label"));

        var streamId = await grain.GetStreamId();
        var events = await FetchStreamEvents(streamId);

        events.Should().HaveCount(2);
        events[0].Should().BeOfType<CounterIncremented>().Which.Amount.Should().Be(10);
        events[1].Should().BeOfType<LabelChanged>().Which.Label.Should().Be("tx-label");
    }

    [Fact]
    public async Task TransactionalAppend_Rollback_NoEventsWritten()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IEventTestGrain>(id);

        var transactions = GetSiloService<ITransactions>();
        var result = await transactions.Run(async () => await grain.AppendThenFail(99));

        result.IsSuccess.Should().BeFalse();

        var streamId = await grain.GetStreamId();
        var events = await FetchStreamEvents(streamId);

        events.Should().BeEmpty();
    }

    // --- Mixed direct + event state in transaction ---

    [Fact]
    public async Task MixedStateTransaction_BothCommitted()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IMixedStateGrain>(id);

        await RunTransaction(() => grain.IncrementAndAppend(7));

        var directValue = await grain.GetDirectValue();
        directValue.Should().Be(1);

        var streamId = await grain.GetStreamId();
        var events = await FetchStreamEvents(streamId);
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<CounterIncremented>().Which.Amount.Should().Be(7);
    }

    [Fact]
    public async Task MixedStateTransaction_Failure_BothRolledBack()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IMixedStateGrain>(id);

        var transactions = GetSiloService<ITransactions>();
        var result = await transactions.Run(async () => await grain.IncrementAndAppendThenFail(7));

        result.IsSuccess.Should().BeFalse();

        var directValue = await grain.GetDirectValue();
        directValue.Should().Be(0);

        var streamId = await grain.GetStreamId();
        var events = await FetchStreamEvents(streamId);
        events.Should().BeEmpty();
    }

    // --- Multiple event-sourced grains in transaction ---

    [Fact]
    public async Task Transaction_TwoEventGrains_BothCommitted()
    {
        var grainA = GetGrain<IEventTestGrain>(Guid.NewGuid());
        var grainB = GetGrain<IEventTestGrain>(Guid.NewGuid());

        await RunTransaction(async () => {
            await grainA.AppendEvents(1, "a");
            await grainB.AppendEvents(2, "b");
        });

        var streamA = await grainA.GetStreamId();
        var streamB = await grainB.GetStreamId();

        var eventsA = await FetchStreamEvents(streamA);
        var eventsB = await FetchStreamEvents(streamB);

        eventsA.Should().HaveCount(2);
        eventsB.Should().HaveCount(2);
    }

    [Fact]
    public async Task Transaction_TwoEventGrains_Failure_BothRolledBack()
    {
        var grainA = GetGrain<IEventTestGrain>(Guid.NewGuid());
        var grainB = GetGrain<IEventTestGrain>(Guid.NewGuid());

        var transactions = GetSiloService<ITransactions>();
        var result = await transactions.Run(async () => {
            await grainA.AppendEvents(1, "a");
            await grainB.AppendEvents(2, "b");
            throw new Exception("fail after both");
        });

        result.IsSuccess.Should().BeFalse();

        var streamA = await grainA.GetStreamId();
        var streamB = await grainB.GetStreamId();

        var eventsA = await FetchStreamEvents(streamA);
        var eventsB = await FetchStreamEvents(streamB);

        eventsA.Should().BeEmpty();
        eventsB.Should().BeEmpty();
    }

    // --- Multiple appends on same grain within transaction ---

    [Fact]
    public async Task Transaction_SameGrainMultipleAppends_AllEventsCommitted()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IEventTestGrain>(id);

        await RunTransaction(async () => {
            await grain.AppendEvents(1, "first");
            await grain.AppendEvents(2, "second");
        });

        var streamId = await grain.GetStreamId();
        var events = await FetchStreamEvents(streamId);

        events.Should().HaveCount(4);
    }

    // --- Error cases ---

    [Fact]
    public async Task AppendWithoutRead_ThrowsInvalidOperationException()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IEventTestGrain>(id);

        var act = () => grain.AppendWithoutRead();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Append_MissingApplyMethod_ThrowsInvalidOperationException()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IEventTestGrain>(id);

        var act = () => grain.AppendNoApplyEvent();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Write_NoPendingEvents_NoOp()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IEventTestGrain>(id);

        var act = () => grain.WriteWithoutPending();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Transaction_MultipleWrites_AllEventsCommitted()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IEventTestGrain>(id);

        await RunTransaction(() => grain.AppendTwiceInTransaction());

        var streamId = await grain.GetStreamId();
        var events = await FetchStreamEvents(streamId);

        events.Should().HaveCount(2);
        events[0].Should().BeOfType<CounterIncremented>().Which.Amount.Should().Be(1);
        events[1].Should().BeOfType<CounterIncremented>().Which.Amount.Should().Be(2);
    }

    [Fact]
    public async Task Standalone_ConcurrentAppends_NoRaceCondition()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IEventTestGrain>(id);

        // Prime the state
        await grain.WriteWithoutPending();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => grain.AppendEventsStandalone(1, "c"))
            .ToList();

        await Task.WhenAll(tasks);

        var counter = await grain.GetCounter();
        counter.Should().Be(10);
    }

    // --- Reusability after rollback ---

    [Fact]
    public async Task Transaction_RollbackThenSuccess_GrainReusable()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IEventTestGrain>(id);

        var transactions = GetSiloService<ITransactions>();
        var failResult = await transactions.Run(async () => await grain.AppendThenFail(42));
        failResult.IsSuccess.Should().BeFalse();

        await RunTransaction(() => grain.AppendEvents(7, "ok"));

        var streamId = await grain.GetStreamId();
        var events = await FetchStreamEvents(streamId);

        events.Should().HaveCount(2);
        events[0].Should().BeOfType<CounterIncremented>().Which.Amount.Should().Be(7);
    }

    // --- Helpers ---

    private async Task<IReadOnlyList<object>> FetchStreamEvents(string streamId)
    {
        var store = GetSiloService<IDocumentStore>();
        await using var session = store.QuerySession();
        var stream = await session.Events.FetchStreamAsync(streamId);
        return stream?.Select(e => e.Data).ToList() ?? new List<object>();
    }
}
