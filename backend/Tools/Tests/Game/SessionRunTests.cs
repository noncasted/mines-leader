using Common.Reactive;
using FluentAssertions;
using Shared;
using Tests.Network;
using Xunit;
using static Tests.Game.SessionCloseTests;

namespace Tests.Game;

/// <summary>
/// N9 (port of peace-data-reborn F2.13): a session with ExpectedUsers starts handling its users when the last
/// expected one joins (no poll), and handles a user that joins after the start as well (a second connection of a
/// player): before, such a user was never handled and its socket hung. Real Session on the real execution queue.
/// </summary>
public class SessionRunTests
{
    static SessionRunTests()
    {
        NetworkContextsSetup.Register();
    }

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task NoExpectedUsers_UserIsHandledOnJoin()
    {
        var world = new SessionWorld(SessionType.Lobby, expectedUsers: 0);
        world.Session.Run();

        world.ConnectUser(out var socket);

        await WaitFor<SharedSessionPlayer.LocalUpdate>(socket);
    }

    [Fact]
    public async Task ExpectedUsers_NotAllJoined_NobodyIsHandled()
    {
        var world = new SessionWorld(SessionType.Match, expectedUsers: 2);
        var allConnected = false;
        world.Session.AllUsersConnected.Advise(world.Session.Lifetime, () => allConnected = true);
        world.Session.Run();

        world.ConnectUser(out var first);
        await Task.Delay(world.Data.JoinTimeout / 3);

        first.DrainSent().Should().BeEmpty();
        allConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ExpectedUsers_LastOneJoins_EveryoneIsHandled()
    {
        var world = new SessionWorld(SessionType.Match, expectedUsers: 2);
        var allConnected = world.Session.AllUsersConnected.WaitInvoke(world.Session.Lifetime);
        world.Session.Run();

        world.ConnectUser(out var first);
        world.ConnectUser(out var second);

        await allConnected.WaitAsync(Wait);
        await WaitFor<SharedSessionPlayer.LocalUpdate>(first);
        await WaitFor<SharedSessionPlayer.LocalUpdate>(second);
    }

    [Fact]
    public async Task ExpectedUsers_UserJoiningAfterStart_IsHandled_OthersLearnAboutIt()
    {
        var world = new SessionWorld(SessionType.Match, expectedUsers: 1);
        var allConnected = world.Session.AllUsersConnected.WaitInvoke(world.Session.Lifetime);
        world.Session.Run();

        world.ConnectUser(out var first);
        await allConnected.WaitAsync(Wait);
        var firstLocal = await WaitFor<SharedSessionPlayer.LocalUpdate>(first);

        world.ConnectUser(out var late);

        var lateLocal = await WaitFor<SharedSessionPlayer.LocalUpdate>(late);
        await WaitFor<SharedSessionPlayer.RemoteUpdate>(late, update => update.Index == firstLocal.Index);
        await WaitFor<SharedSessionPlayer.RemoteUpdate>(first, update => update.Index == lateLocal.Index);

        // Handled once: the late join does not handle the first user again.
        first.DrainSent().OfType<SharedSessionPlayer.LocalUpdate>().Should().BeEmpty();
    }

    [Fact]
    public async Task ExpectedUsers_SessionEndsBeforeJoin_RunCompletes_NobodyIsHandled()
    {
        var world = new SessionWorld(SessionType.Match, expectedUsers: 2);
        var allConnected = false;
        world.Session.AllUsersConnected.Advise(world.Session.Lifetime, () => allConnected = true);
        var run = world.Session.Run();

        world.ConnectUser(out var first);
        world.Data.Lifetime.Terminate();

        await run.WaitAsync(Wait);
        allConnected.Should().BeFalse();
        first.DrainSent().Should().BeEmpty();
    }

    private static async Task<T> WaitFor<T>(ScriptedSocket socket, Func<T, bool>? predicate = null)
        where T : INetworkContext
    {
        while (true)
        {
            var context = await socket.ReadSent(Wait);

            if (context is T typed && (predicate == null || predicate(typed) == true))
                return typed;
        }
    }
}
