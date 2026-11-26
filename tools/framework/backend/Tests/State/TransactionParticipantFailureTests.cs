using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

// A state that fails inside a transaction before it reaches the handler must still be rolled back by it: otherwise
// the state keeps the id of a dead transaction and every later transaction of the grain fails.
public class TransactionParticipantFailureTests : IDisposable
{
    public TransactionParticipantFailureTests()
    {
        var config = Substitute.For<ITransactionConfig>();
        config.Value.Returns(new TransactionOptions());
        _handler = new GrainTransactionHandler(config, NullLogger<GrainTransactionHandler>.Instance);

        _context = Substitute.For<IGrainContext>();
        _context.GrainId.Returns(GrainId.Create(GrainType.Create("test"),
            GrainIdKeyExtensions.CreateGuidKey(Guid.NewGuid())));
        _context.GetComponent<IGrainTransactionHandler>().Returns(_handler);
    }

    private readonly GrainTransactionHandler _handler;
    private readonly IGrainContext _context;

    public void Dispose()
    {
        TransactionContextProvider.Clear();
    }

    [Fact]
    public async Task State_FailedReadInTransaction_NextTransactionReads()
    {
        var storage = Substitute.For<IStateStorage>();
        storage.Registry.Returns(TestStates.CreateRegistry());

        storage.Read<TxTestState>(Arg.Any<StateIdentity>())
               .Returns(_ => Task.FromException<TxTestState>(new Exception("read failed")),
                   _ => Task.FromResult(TxTestState.Of(7)));

        var state = new State<TxTestState>(storage, _context);

        var failed = Begin();
        await _handler.Join(failed);
        var read = () => state.Read();
        await read.Should().ThrowAsync<Exception>().WithMessage("read failed");
        await _handler.OnFailure(failed);

        await _handler.Join(Begin());
        await state.Read();

        state.Value.Value.Should().Be(7);
    }

    [Fact]
    public async Task EventState_FailedLoadInTransaction_NextTransactionLoads()
    {
        var storage = Substitute.For<IEventStorage>();

        storage.Read<EventTestAggregate>(Arg.Any<string>())
               .Returns(_ => Task.FromException<EventTestAggregate>(new Exception("load failed")),
                   _ => {
                       var stored = new EventTestAggregate();
                       stored.Apply(new CounterIncremented { Amount = 7 });
                       return Task.FromResult(stored);
                   });

        var state = CreateEventState(storage);

        var failed = Begin();
        await _handler.Join(failed);
        var load = () => state.Load();
        await load.Should().ThrowAsync<Exception>().WithMessage("load failed");
        await _handler.OnFailure(failed);

        await _handler.Join(Begin());
        await state.Load();

        state.Value.Counter.Should().Be(7);
    }

    [Fact]
    public async Task EventState_AppendWithoutWriteRolledBack_EventsNotKept()
    {
        var storage = Substitute.For<IEventStorage>();
        storage.Read<EventTestAggregate>(Arg.Any<string>()).Returns(_ => Task.FromResult(new EventTestAggregate()));

        var state = CreateEventState(storage);

        // Loaded outside a transaction, appended inside one that fails before Write.
        await state.Load();
        var failed = Begin();
        await _handler.Join(failed);
        await state.Append(new CounterIncremented { Amount = 5 });
        await _handler.OnFailure(failed);

        TransactionContextProvider.Clear();
        await state.Load();

        state.Value.Counter.Should().Be(0);
    }

    private EventState<EventTestAggregate> CreateEventState(IEventStorage storage)
    {
        return new EventState<EventTestAggregate>(storage, _context, TestStates.CreateRegistry(),
            Substitute.For<IStateSerializer>());
    }

    // Not async: the transaction context is an AsyncLocal, set here it must flow back to the test.
    private Guid Begin()
    {
        var context = new TransactionContext { Id = Guid.NewGuid() };
        TransactionContextProvider.SetCurrent(context);
        return context.Id;
    }
}
