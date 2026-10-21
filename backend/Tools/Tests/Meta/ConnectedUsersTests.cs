using Cluster.Deploy;
using Cluster.Monitoring;
using Common.Reactive;
using FluentAssertions;
using MetaGateway.UserFlow;
using MetaGateway.UserFlow.Connection;
using NSubstitute;
using Xunit;

namespace Tests.Meta;

public class ConnectedUsersTests
{
    private readonly ConnectedUsers _sut = new(Substitute.For<IServiceSlicedState<ConnectedUsersLiveData, List<ConnectedUserEntry>>>());

    private static IUserSession CreateSession(Guid? userId = null, Lifetime? lifetime = null)
    {
        var session = Substitute.For<IUserSession>();
        session.UserId.Returns(userId ?? Guid.NewGuid());
        var lt = lifetime ?? new Lifetime();
        session.Lifetime.Returns(lt);
        return session;
    }

    [Fact]
    public void Add_Session_MarksUserConnected()
    {
        var session = CreateSession();

        _sut.Add(session);

        _sut.IsConnected(session.UserId).Should().BeTrue();
        _sut.Entries.Should().ContainKey(session.UserId);
    }

    [Fact]
    public void Remove_Session_MarksUserDisconnected()
    {
        var session = CreateSession();
        _sut.Add(session);

        _sut.Remove(session);

        _sut.IsConnected(session.UserId).Should().BeFalse();
        _sut.Entries.Should().NotContainKey(session.UserId);
    }

    [Fact]
    public void IsConnected_UnknownUser_ReturnsFalse()
    {
        _sut.IsConnected(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Connected_Event_FiresOnAdd()
    {
        var lifetime = new Lifetime();
        IUserSession? received = null;
        _sut.Connected.Advise(lifetime, session => received = session);

        var session = CreateSession();
        _sut.Add(session);

        received.Should().BeSameAs(session);
    }

    [Fact]
    public void Remove_NonExistent_DoesNotThrow()
    {
        var session = CreateSession();

        var act = () => _sut.Remove(session);

        act.Should().NotThrow();
    }

    [Fact]
    public void Multiple_Sessions_IndependentTracking()
    {
        var session1 = CreateSession();
        var session2 = CreateSession();

        _sut.Add(session1);
        _sut.Add(session2);

        _sut.IsConnected(session1.UserId).Should().BeTrue();
        _sut.IsConnected(session2.UserId).Should().BeTrue();

        _sut.Remove(session1);

        _sut.IsConnected(session1.UserId).Should().BeFalse();
        _sut.IsConnected(session2.UserId).Should().BeTrue();
    }

    [Fact]
    public void Lifetime_Termination_RemovesFromEntries()
    {
        var lifetime = new Lifetime();
        var session = CreateSession(lifetime: lifetime);

        _sut.Add(session);
        _sut.IsConnected(session.UserId).Should().BeTrue();

        lifetime.Terminate();

        _sut.IsConnected(session.UserId).Should().BeFalse();
        _sut.Entries.Should().NotContainKey(session.UserId);
    }

    [Fact]
    public void Entries_ReturnsAllConnectedSessions()
    {
        var session1 = CreateSession();
        var session2 = CreateSession();

        _sut.Add(session1);
        _sut.Add(session2);

        _sut.Entries.Should().HaveCount(2);
        _sut.Entries[session1.UserId].Should().BeSameAs(session1);
        _sut.Entries[session2.UserId].Should().BeSameAs(session2);
    }

    [Fact]
    public void Add_SameUser_ReplacesAndReturnsPrevious()
    {
        var userId = Guid.NewGuid();
        var first = CreateSession(userId);
        var second = CreateSession(userId);

        _sut.Add(first).Should().BeNull();
        _sut.Add(second).Should().BeSameAs(first);

        _sut.Entries[userId].Should().BeSameAs(second);
    }

    [Fact]
    public void ReplacedSession_LifetimeEnd_KeepsNewSession()
    {
        var userId = Guid.NewGuid();
        var firstLifetime = new Lifetime();
        var first = CreateSession(userId, firstLifetime);
        var second = CreateSession(userId);

        _sut.Add(first);
        _sut.Add(second);
        firstLifetime.Terminate();

        _sut.Entries[userId].Should().BeSameAs(second);
    }

    [Fact]
    public void Add_TerminatedLifetime_RemovedRightAway()
    {
        var lifetime = new Lifetime();
        lifetime.Terminate();

        var session = CreateSession(lifetime: lifetime);
        _sut.Add(session);

        _sut.IsConnected(session.UserId).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentAddAndDisconnect_EntriesAndLastLiveDataConsistent()
    {
        var liveData = Substitute.For<IServiceSlicedState<ConnectedUsersLiveData, List<ConnectedUserEntry>>>();
        List<ConnectedUserEntry>? lastSlice = null;
        liveData.SetSlice(Arg.Do<List<ConnectedUserEntry>>(slice => lastSlice = slice));

        var sut = new ConnectedUsers(liveData);
        var kept = new System.Collections.Concurrent.ConcurrentBag<Guid>();

        var workers = Enumerable.Range(0, 8).Select(worker => Task.Run(() => {
            for (var i = 0; i < 500; i++)
            {
                var lifetime = new Lifetime();
                var session = CreateSession(lifetime: lifetime);
                sut.Add(session);

                if (i % 5 == 0)
                {
                    kept.Add(session.UserId);
                    continue;
                }

                lifetime.Terminate();
            }
        }, TestContext.Current.CancellationToken));

        await Task.WhenAll(workers);

        sut.Entries.Keys.Should().BeEquivalentTo(kept);
        lastSlice.Should().NotBeNull();
        lastSlice!.Select(entry => entry.UserId).Should().BeEquivalentTo(kept);
    }
}
