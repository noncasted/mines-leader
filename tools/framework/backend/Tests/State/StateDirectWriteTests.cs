using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

// The writes of a State outside a transaction (StateExtensions.Write): the memory of the grain follows the database.
public class StateDirectWriteTests
{
    public StateDirectWriteTests()
    {
        _storage.Registry.Returns(TestStates.CreateRegistry());
        _storage.Read<TxTestState>(Arg.Any<StateIdentity>()).Returns(_ => Task.FromResult(TxTestState.Of(1)));

        var context = Substitute.For<IGrainContext>();
        context.GrainId.Returns(GrainId.Create(GrainType.Create("test"),
            GrainIdKeyExtensions.CreateGuidKey(Guid.NewGuid())));

        _state = new State<TxTestState>(_storage, context);
    }

    private readonly IStateStorage _storage = Substitute.For<IStateStorage>();
    private readonly State<TxTestState> _state;

    [Fact]
    public async Task Write_Fails_MemoryNotChanged()
    {
        _storage.Write(Arg.Any<StateWriteRequest>()).Returns(Task.FromException(new Exception("db down")));

        var write = () => _state.Write(s => s.SetValue(5));
        await write.Should().ThrowAsync<Exception>().WithMessage("db down");

        var value = await _state.Read(s => s.Value);
        value.Should().Be(1, "the change that is not in the database must not stay in memory");
    }

    [Fact]
    public async Task Write_Interleaved_LastDatabaseWriteIsNewest()
    {
        // A reentrant grain: the second write starts while the first one is still in flight.
        var database = new List<int>();
        var first = new TaskCompletionSource();
        var calls = 0;

        _storage.Write(Arg.Any<StateWriteRequest>()).Returns(call => {
            var value = ((TxTestState)call.Arg<StateWriteRequest>().Records.Values.Single()).Value;

            if (Interlocked.Increment(ref calls) == 1)
                return first.Task.ContinueWith(_ => database.Add(value), TaskScheduler.Default);

            database.Add(value);
            return Task.CompletedTask;
        });

        var firstWrite = _state.Write(s => s.SetValue(2));
        var secondWrite = _state.Write(s => s.SetValue(3));

        first.SetResult();
        await Task.WhenAll(firstWrite, secondWrite);

        database.Last().Should().Be(_state.Value.Value, "the database ends with the value the grain has in memory");
    }
}
