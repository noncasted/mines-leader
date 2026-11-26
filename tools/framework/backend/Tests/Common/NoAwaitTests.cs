using Common.Extensions;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Common;

// A task nobody awaits has no caller to throw to: its failure goes to NoAwaitExceptions.Handler when it happens.
[Collection(nameof(NoAwaitCollection))]
public class NoAwaitTests : NoAwaitHandlerTest
{
    [Fact]
    public void NoAwait_TaskFails_ReportedAtOnce()
    {
        var completion = new TaskCompletionSource();
        var error = new InvalidOperationException("loop died");

        completion.Task.NoAwait();
        completion.SetException(error);

        Reported.Should().ContainSingle().Which.Should().BeSameAs(error);
    }

    [Fact]
    public void NoAwait_TaskCancelled_NotReported()
    {
        var completion = new TaskCompletionSource();

        completion.Task.NoAwait();
        completion.SetCanceled(TestContext.Current.CancellationToken);

        Reported.Should().BeEmpty();
    }
}
