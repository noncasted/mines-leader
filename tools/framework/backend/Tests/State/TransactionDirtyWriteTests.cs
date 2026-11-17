using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Marten;
using Xunit;

namespace Framework.Tests.State;

// A call of a grain outside a transaction, made between the steps of an open transaction of that grain: its memory holds
// the changes of the transaction, so a write there would store them before the commit, and a rollback would leave them
// in the database.
[Collection(nameof(OrleansIntegrationCollection))]
public class TransactionDirtyWriteTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task DirectWriteDuringOpenTransaction_RolledBackChangeNotStored()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var transactions = GetSiloService<ITransactions>();
        var hold = TestGate.Create();

        var failing = transactions.Run(async () => {
            await grain.IncrementAndHold(hold.Id);
            throw new Exception("Intentional rollback");
        });

        await hold.Entered;
        var direct = await Record.ExceptionAsync(() => grain.IncrementDirect());
        hold.Release();

        (await failing).IsSuccess.Should().BeFalse();

        await grain.DeactivateAndWaitNewActivation();
        (await grain.Get()).Should().Be(0, "the change of the rolled back transaction must not reach the database");
        direct.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task DirectAppendDuringOpenTransaction_RolledBackEventNotStored()
    {
        var grain = GetGrain<IEventTestGrain>(Guid.NewGuid());
        var transactions = GetSiloService<ITransactions>();
        var hold = TestGate.Create();

        var failing = transactions.Run(async () => {
            await grain.AppendAndHold(7, hold.Id);
            throw new Exception("Intentional rollback");
        });

        await hold.Entered;
        var direct = await Record.ExceptionAsync(() => grain.AppendEventsStandalone(1, "direct"));
        hold.Release();

        (await failing).IsSuccess.Should().BeFalse();

        var events = await FetchStreamEvents(await grain.GetStreamId());
        events.OfType<CounterIncremented>().Should().NotContain(e => e.Amount == 7,
            "the event of the rolled back transaction must not reach the stream");
        direct.Should().BeOfType<InvalidOperationException>();
    }

    private async Task<IReadOnlyList<object>> FetchStreamEvents(string streamId)
    {
        var store = GetSiloService<IDocumentStore>();
        await using var session = store.QuerySession();
        var stream = await session.Events.FetchStreamAsync(streamId);
        return stream.Select(e => e.Data).ToList();
    }
}
