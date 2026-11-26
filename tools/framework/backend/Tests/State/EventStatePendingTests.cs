using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.State;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

// An EventState outside a transaction: the events appended and applied to the value stay pending until Write, a Read
// in between does not drop them while keeping their effect on the value.
public class EventStatePendingTests
{
    [Fact]
    public async Task ReadBetweenAppendAndWrite_EventsStillWritten()
    {
        TransactionContextProvider.Clear();

        var storage = Substitute.For<IEventStorage>();
        storage.Read<EventTestAggregate>(Arg.Any<string>()).Returns(_ => Task.FromResult(new EventTestAggregate()));

        var context = Substitute.For<IGrainContext>();
        context.GrainId.Returns(GrainId.Create(GrainType.Create("test"),
            GrainIdKeyExtensions.CreateGuidKey(Guid.NewGuid())));

        var state = new EventState<EventTestAggregate>(storage, context, TestStates.CreateRegistry(),
            Substitute.For<IStateSerializer>());

        await state.Read();
        await state.Append(new CounterIncremented { Amount = 5 });

        var value = await state.Read();
        await state.Write();

        value.Counter.Should().Be(5);
        await storage.Received(1).Append(Arg.Any<string>(), Arg.Is<object[]>(events => events.Length == 1));
    }
}
