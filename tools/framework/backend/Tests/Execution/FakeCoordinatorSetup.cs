using Internal;
using FluentAssertions;
using Infrastructure;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Execution;

internal class FakeCoordinatorSetup : ICoordinatorSetupCompleted
{
    public bool WasCalled { get; private set; }

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        WasCalled = true;
        return Task.CompletedTask;
    }
}
