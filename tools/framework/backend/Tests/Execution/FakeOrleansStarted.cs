using Internal;
using FluentAssertions;
using Infrastructure;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Execution;

internal class FakeOrleansStarted : IOrleansStarted
{
    public bool WasCalled { get; private set; }
    public IReadOnlyLifetime? ReceivedLifetime { get; private set; }

    public Task OnOrleansStarted(IReadOnlyLifetime lifetime)
    {
        WasCalled = true;
        ReceivedLifetime = lifetime;
        return Task.CompletedTask;
    }
}
