using Internal;
using FluentAssertions;
using Infrastructure;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Execution;

internal class FakeLocalSetup : ILocalSetupCompleted
{
    public bool WasCalled { get; private set; }

    public Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        WasCalled = true;
        return Task.CompletedTask;
    }
}
