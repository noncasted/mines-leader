using FluentAssertions;
using Infrastructure;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Loop;

// IsOrleansStarted turns true when the silo or the client has started, not when its lifecycle is built.
public class ServiceLoopObserverTests
{
    [Fact]
    public async Task Silo_StartedOnlyAtActiveStage()
    {
        var observer = new ServiceLoopObserver();
        var lifecycle = Substitute.For<ISiloLifecycle>();
        var stages = new List<(int Stage, ILifecycleObserver Observer)>();
        lifecycle.Subscribe(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<ILifecycleObserver>())
                 .Returns(call => {
                     stages.Add((call.ArgAt<int>(1), call.ArgAt<ILifecycleObserver>(2)));
                     return Substitute.For<IDisposable>();
                 });

        observer.Participate(lifecycle);

        observer.IsOrleansStarted.Value.Should().BeFalse("the silo has not started yet");

        stages.Should().ContainSingle().Which.Stage.Should().Be(ServiceLifecycleStage.Active);
        await stages[0].Observer.OnStart(TestContext.Current.CancellationToken);

        observer.IsOrleansStarted.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Client_StartedOnlyAtActiveStage()
    {
        var observer = new ServiceLoopObserver();
        var lifecycle = Substitute.For<IClusterClientLifecycle>();
        var stages = new List<(int Stage, ILifecycleObserver Observer)>();
        lifecycle.Subscribe(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<ILifecycleObserver>())
                 .Returns(call => {
                     stages.Add((call.ArgAt<int>(1), call.ArgAt<ILifecycleObserver>(2)));
                     return Substitute.For<IDisposable>();
                 });

        observer.Participate(lifecycle);

        observer.IsOrleansStarted.Value.Should().BeFalse("the client has not connected yet");

        stages.Should().ContainSingle().Which.Stage.Should().Be(ServiceLifecycleStage.Active);
        await stages[0].Observer.OnStart(TestContext.Current.CancellationToken);

        observer.IsOrleansStarted.Value.Should().BeTrue();
    }
}
