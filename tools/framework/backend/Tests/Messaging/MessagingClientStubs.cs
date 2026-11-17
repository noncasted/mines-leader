using Framework.Tests.Fixtures;
using Framework.Tests.State.Storages;
using Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Messaging;

// What the unit tests of the messaging clients share: an IOrleans that gives one substituted grain and keeps the
// observers the client created, and a wait for a condition reached by a task nobody awaits.
public static class MessagingClientStubs
{
    public static IOrleans OrleansOf<TGrain, TObserver>(TGrain grain, List<IGrainObserver> createdObservers)
        where TGrain : class, IGrainWithStringKey
        where TObserver : class, IGrainObserver
    {
        var orleans = Substitute.For<IOrleans>();

        orleans.Client.GetGrain<TGrain>(Arg.Any<string>(), Arg.Any<string>()).Returns(grain);

        orleans.Client.CreateObjectReference<TObserver>(Arg.Any<IGrainObserver>()).Returns(call => {
            lock (createdObservers)
                createdObservers.Add(call.Arg<IGrainObserver>());

            return Substitute.For<TObserver>();
        });

        return orleans;
    }

    public static IServiceProvider ServicesWith<TConfig>(TConfig config)
        where TConfig : class
    {
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(TConfig)).Returns(config);
        return services;
    }

    // Starts the two together on two threads, to meet in a window of a few instructions.
    public static async Task Race(Func<Task> first, Func<Task> second)
    {
        var ready = 0;

        await Task.WhenAll(Task.Run(() => Together(first)), Task.Run(() => Together(second)));

        Task Together(Func<Task> action)
        {
            Interlocked.Increment(ref ready);

            while (Volatile.Read(ref ready) < 2)
                Thread.SpinWait(1);

            return action();
        }
    }
}
