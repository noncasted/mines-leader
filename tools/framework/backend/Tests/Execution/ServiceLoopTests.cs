using Internal;
using FluentAssertions;
using Infrastructure;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Execution;

public class ServiceLoopTests : LifetimeTest
{
    [Fact]
    public async Task OnOrleansStarted_CallsAllParticipants()
    {
        var p1 = new FakeOrleansStarted();
        var p2 = new FakeOrleansStarted();

        var loop = new ServiceLoop(new IOrleansStarted[] { p1, p2 },
            Array.Empty<ILocalSetupCompleted>(),
            Array.Empty<ICoordinatorSetupCompleted>(),
            Array.Empty<IServiceStarted>());

        var lifetime = TestLifetime.Child();
        await loop.OnOrleansStarted(lifetime);

        p1.WasCalled.Should().BeTrue();
        p2.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnLocalSetupCompleted_CallsAllParticipants()
    {
        var p1 = new FakeLocalSetup();
        var p2 = new FakeLocalSetup();

        var loop = new ServiceLoop(Array.Empty<IOrleansStarted>(),
            new ILocalSetupCompleted[] { p1, p2 },
            Array.Empty<ICoordinatorSetupCompleted>(),
            Array.Empty<IServiceStarted>());

        var lifetime = TestLifetime.Child();
        await loop.OnLocalSetupCompleted(lifetime);

        p1.WasCalled.Should().BeTrue();
        p2.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnCoordinatorSetupCompleted_CallsAllParticipants()
    {
        var p1 = new FakeCoordinatorSetup();
        var p2 = new FakeCoordinatorSetup();

        var loop = new ServiceLoop(Array.Empty<IOrleansStarted>(),
            Array.Empty<ILocalSetupCompleted>(),
            new ICoordinatorSetupCompleted[] { p1, p2 },
            Array.Empty<IServiceStarted>());

        var lifetime = TestLifetime.Child();
        await loop.OnCoordinatorSetupCompleted(lifetime);

        p1.WasCalled.Should().BeTrue();
        p2.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnOrleansStarted_PassesLifetimeToParticipants()
    {
        var participant = new FakeOrleansStarted();

        var loop = new ServiceLoop(new IOrleansStarted[] { participant },
            Array.Empty<ILocalSetupCompleted>(),
            Array.Empty<ICoordinatorSetupCompleted>(),
            Array.Empty<IServiceStarted>());

        var lifetime = TestLifetime.Child();
        await loop.OnOrleansStarted(lifetime);

        participant.ReceivedLifetime.Should().BeSameAs(lifetime);
    }

    [Fact]
    public async Task HandlerFailure_DoesNotBlockOtherParticipants()
    {
        // ServiceLoop uses Task.WhenAll — when a handler returns a faulted Task,
        // other handlers still execute
        var failing = new FailingOrleansStarted();
        var successful = new FakeOrleansStarted();

        var loop = new ServiceLoop(new IOrleansStarted[] { failing, successful },
            Array.Empty<ILocalSetupCompleted>(),
            Array.Empty<ICoordinatorSetupCompleted>(),
            Array.Empty<IServiceStarted>());

        var lifetime = TestLifetime.Child();
        var act = () => loop.OnOrleansStarted(lifetime);

        await act.Should().ThrowAsync<InvalidOperationException>();

        // The successful handler still ran (Task.WhenAll runs all in parallel)
        successful.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task AllStages_IndependentOfEachOther()
    {
        var orleans = new FakeOrleansStarted();
        var local = new FakeLocalSetup();
        var coordinator = new FakeCoordinatorSetup();

        var loop = new ServiceLoop(new IOrleansStarted[] { orleans },
            new ILocalSetupCompleted[] { local },
            new ICoordinatorSetupCompleted[] { coordinator },
            Array.Empty<IServiceStarted>());

        var lifetime = TestLifetime.Child();

        // Only call OnOrleansStarted
        await loop.OnOrleansStarted(lifetime);

        orleans.WasCalled.Should().BeTrue();
        local.WasCalled.Should().BeFalse();
        coordinator.WasCalled.Should().BeFalse();
    }
}
