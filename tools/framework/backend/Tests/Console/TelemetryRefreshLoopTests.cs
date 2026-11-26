using Console.Infrastructure.ClientTelemetry;
using Framework.Tests.Fixtures;
using Internal;
using Xunit;

namespace Framework.Tests.Console;

public class TelemetryRefreshLoopTests
{
    [Fact]
    public async Task AFailedFirstRead_IsRetried_UntilItSucceeds_ThenTheLoopEnds()
    {
        var reads = 0;
        var loop = new TelemetryRefreshLoop(
            () => ++reads < 3 ? throw new InvalidOperationException("database down") : Task.CompletedTask,
            () => false,
            () => Task.CompletedTask,
            TimeSpan.FromMilliseconds(1));
        var lifetime = new Lifetime();

        try
        {
            await loop.Refresh();
            Assert.Equal("database down", loop.Error);

            await loop.Run(lifetime).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            Assert.Equal(3, reads);
            Assert.Null(loop.Error);
        }
        finally
        {
            lifetime.Terminate();
        }
    }
}
