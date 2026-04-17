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
    private readonly ConnectedUsers _sut = new(Substitute.For<ILiveState<ConnectedUsersLiveData>>());

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
}