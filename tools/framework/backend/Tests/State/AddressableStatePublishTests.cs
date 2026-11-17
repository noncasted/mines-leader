using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Framework.Tests.State;

// The value is written before it is published: a publish that fails once must not leave the other hosts with the old
// value, and one that keeps failing must say that the value is saved.
[Collection(nameof(OrleansIntegrationCollection))]
public class AddressableStatePublishTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task PublishFailsOnce_Retried()
    {
        var channel = Substitute.For<IRuntimeChannelClient>();
        var calls = 0;

        channel.Publish(Arg.Any<IRuntimeChannelId>(), Arg.Any<object>()).Returns(_ => {
            if (Interlocked.Increment(ref calls) == 1)
                throw new TimeoutException("channel grain busy");

            return Task.CompletedTask;
        });

        var state = CreateState(channel);
        await state.SetValue(new AddressableStorageTestOptions { Label = "published", Limit = 1 });

        calls.Should().Be(2, "a failed publish is retried");
        state.Value.Label.Should().Be("published");
    }

    [Fact]
    public async Task PublishAlwaysFails_ErrorSaysSaved()
    {
        var channel = Substitute.For<IRuntimeChannelClient>();
        channel.Publish(Arg.Any<IRuntimeChannelId>(), Arg.Any<object>()).ThrowsAsync(new TimeoutException("down"));

        var state = CreateState(channel);
        var act = () => state.SetValue(new AddressableStorageTestOptions { Label = "saved", Limit = 2 });

        (await act.Should().ThrowAsync<Exception>()).Which.Message.Should().Contain("saved");
        state.Value.Label.Should().Be("saved", "the value is in the database and applied on this host");
    }

    private AddressableStorageTestState CreateState(IRuntimeChannelClient channel)
    {
        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimeChannel.Returns(channel);

        var utils = new AddressableStateUtils(GetSiloService<IOrleans>(), messaging, GetSiloService<ILoggerFactory>());
        return new AddressableStorageTestState(utils);
    }
}
