using Cluster.Deploy;
using Cluster.Monitoring;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.Hosting.Collection;
using Game.Session;
using Framework.Tests.Session;
using Internal;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Hosting;

// The live sessions of a game server as the cluster sees them (LiveSessionsData): the sessions change their users in
// their own queues, on many threads at once, and the slice of the server ends with the latest state.
public class SessionsCollectionTests : LifetimeTest
{
    // The queue of one session has built its snapshot and has not sent it yet when another session changes its
    // users and sends a newer one: the older snapshot must not be the last one sent.
    [Fact]
    public async Task PushesFromTwoSessionQueues_TheLatestStateIsSentLast()
    {
        var slices = new List<List<LiveSessionEntry>>();
        var paused = new ManualResetEventSlim();
        var resume = new ManualResetEventSlim();
        var pausedThread = -1;
        var liveData = Substitute.For<IServiceSlicedState<LiveSessionsData, List<LiveSessionEntry>>>();

        liveData.SetSlice(Arg.Any<List<LiveSessionEntry>>()).Returns(call => {
            // The queue of the first session has built its snapshot and has not handed it over yet.
            if (Environment.CurrentManagedThreadId == Volatile.Read(ref pausedThread))
            {
                paused.Set();
                resume.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            }

            lock (slices)
                slices.Add(call.Arg<List<LiveSessionEntry>>());

            return Task.CompletedTask;
        });

        var collection = new SessionsCollection(liveData, new ManualTimeProvider());

        var (first, firstUsers) = CreateSession();
        var (second, secondUsers) = CreateSession();
        collection.Add(first);
        collection.Add(second);

        var firstQueue = Task.Run(() => {
            Volatile.Write(ref pausedThread, Environment.CurrentManagedThreadId);
            firstUsers.AddUser(CreateUser());
        });

        paused.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

        var userOfSecond = CreateUser();
        var secondQueue = Task.Run(() => secondUsers.AddUser(userOfSecond));
        await Task.WhenAny(secondQueue, Task.Delay(TimeSpan.FromMilliseconds(200)));

        resume.Set();
        await Task.WhenAll(firstQueue, secondQueue);

        List<LiveSessionEntry> last;

        lock (slices)
            last = slices.Last();

        last.Single(entry => entry.Id == second.Id).UserIds.Should().Equal(userOfSecond.BackendId);
        last.Single(entry => entry.Id == first.Id).UserIds.Should().HaveCount(1);
    }

    private (ISession, SessionUsers) CreateSession()
    {
        var users = new SessionUsers();
        var session = Substitute.For<ISession>();
        session.Id.Returns(Guid.NewGuid());
        session.Users.Returns(users);
        session.Lifetime.Returns(new Lifetime(TestLifetime));

        return (session, users);
    }

    private IUser CreateUser()
    {
        var user = Substitute.For<IUser>();
        user.BackendId.Returns(Guid.NewGuid());
        user.Lifetime.Returns(new Lifetime(TestLifetime));
        return user;
    }
}
