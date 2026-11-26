using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Monitoring;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.State;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

// Overlapping SetSlice calls are coalesced: the call that writes the latest slice may fail, and then a call whose
// slice it took must not report success for a slice that was never saved.
public class ServiceSlicedStateCoalescingTests : LifetimeTest
{
    [Fact]
    public async Task WriteOfACoalescedSliceFails_TheOtherCallDoesNotReportSuccessWithoutSavingIt()
    {
        var deployId = Guid.NewGuid();
        var firstWrite = new TaskCompletionSource<AddressableStateValue>();
        var writes = new List<List<ConnectedUserEntry>>();
        var calls = 0;

        var writer = Substitute.For<IServiceSlicesWriter<ConnectedUsersLiveData, List<ConnectedUserEntry>>>();

        writer.SetSlice(Arg.Any<Guid>(), Arg.Any<List<ConnectedUserEntry>>()).Returns(call => {
            writes.Add(call.Arg<List<ConnectedUserEntry>>());

            return ++calls switch
            {
                1 => firstWrite.Task,
                2 => Task.FromException<AddressableStateValue>(new TimeoutException("write failed")),
                _ => Task.FromResult(new AddressableStateValue())
            };
        });

        var orleans = Substitute.For<IOrleans>();
        orleans.StateStorage.Read<AddressableStateValue>(Arg.Any<StateIdentity>())
               .Returns(Task.FromResult(new AddressableStateValue()));
        orleans.Grains.GetGrain<IServiceSlicesWriter<ConnectedUsersLiveData, List<ConnectedUserEntry>>>(deployId)
               .Returns(writer);

        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimeChannel.Returns(new InMemoryChannelClient());

        var state = new ServiceSlicedState<ConnectedUsersLiveData, List<ConnectedUserEntry>>(
            orleans,
            messaging,
            NullLoggerFactory.Instance,
            new ServiceEnvironment { Tag = TestRoles.Meta },
            new ConnectedUsersLiveData());

        await state.OnDeployChanged(deployId, new Lifetime(TestLifetime));

        List<ConnectedUserEntry> first = [new ConnectedUserEntry { UserId = Guid.NewGuid() }];
        List<ConnectedUserEntry> second = [new ConnectedUserEntry { UserId = Guid.NewGuid() }];
        List<ConnectedUserEntry> third = [new ConnectedUserEntry { UserId = Guid.NewGuid() }];

        var firstCall = state.SetSlice(first);
        var secondCall = state.SetSlice(second);
        var thirdCall = state.SetSlice(third);

        firstWrite.SetResult(new AddressableStateValue());
        await firstCall;

        var results = await Task.WhenAll(Outcome(secondCall), Outcome(thirdCall));

        results.Count(failed => failed).Should().Be(1, "only the call whose write failed reports the failure");
        writes.Should().HaveCount(3, "the slice of the failed write is written again for the call that is still waiting");
        writes[1].Should().BeSameAs(third);
        writes[2].Should().BeSameAs(third);
    }

    private static async Task<bool> Outcome(Task call)
    {
        try
        {
            await call;
            return false;
        }
        catch (TimeoutException)
        {
            return true;
        }
    }
}
