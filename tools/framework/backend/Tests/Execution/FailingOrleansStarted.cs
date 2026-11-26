using Internal;
using FluentAssertions;
using Infrastructure;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Execution;

internal class FailingOrleansStarted : IOrleansStarted
{
    public async Task OnOrleansStarted(IReadOnlyLifetime lifetime)
    {
        await Task.Yield();
        throw new InvalidOperationException("Handler failed");
    }
}
